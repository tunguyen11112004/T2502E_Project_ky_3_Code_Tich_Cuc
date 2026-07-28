const { Builder, By, Key, until } = require('selenium-webdriver');
const chrome = require('selenium-webdriver/chrome');
const { MongoClient, ObjectId } = require('mongodb');
const xlsx = require('xlsx');
const path = require('path');
const fs = require('fs');
const os = require('os');

const config = require('./seat-lock-test-data.json');

const sleep = function (ms) { return new Promise(function (resolve) { setTimeout(resolve, ms); }); };
const results = [];
let testCases = [];

function baseUrl() {
    const value = config.app && config.app.baseUrl;
    if (!value) throw new Error('Thieu config.app.baseUrl trong seat-lock-test-data.json');
    return String(value).replace(/\/$/, '');
}

function route(name, fallback) {
    const value = config.routes && config.routes[name];
    if (!value && fallback === undefined) throw new Error('Thieu config.routes.' + name + ' trong seat-lock-test-data.json');
    return value || fallback;
}

function delay(name, fallback) {
    return Number((config.execution && config.execution[name]) !== undefined ? config.execution[name] : fallback);
}

function normalizeText(value) {
    return String(value || '')
        .toLowerCase()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '')
        .trim();
}

function splitFlow(value) {
    return String(value || '')
        .split(';')
        .map(function (item) { return item.trim(); })
        .filter(Boolean);
}

function isEnabled(value) {
    const v = normalizeText(value || 'YES');
    return !['no', 'n', 'false', '0', 'skip', 'disabled', 'khong'].includes(v);
}

function userLabel(userKey) {
    return userKey === 'B' ? 'User B' : 'User A';
}

function isAutoToken(value) {
    const v = normalizeText(value);
    return !v || v === 'auto' || v.startsWith('auto_') || v === 'dynamic' || v === 'generate';
}

function stableHash(value) {
    const text = String(value || '');
    let hash = 0;
    for (let i = 0; i < text.length; i++) {
        hash = ((hash << 5) - hash + text.charCodeAt(i)) >>> 0;
    }
    return hash;
}

function autoAdultDob() {
    const d = new Date();
    d.setFullYear(d.getFullYear() - 24);
    return d.toISOString().slice(0, 10);
}

function autoPassengerData(ctx, userKey) {
    const runStamp = ctx.runStamp || new Date().toISOString().replace(/\D/g, '').slice(0, 14);
    const testId = getTcId(ctx).replace(/[^A-Za-z0-9]/g, '');
    const seed = stableHash(runStamp + '_' + testId + '_' + userKey);
    const phoneBody = String(10000000 + (seed % 89999999)).slice(0, 8);
    const lower = userKey.toLowerCase();

    return {
        name: 'QA Seat Lock ' + userKey + ' ' + testId,
        phone: '09' + phoneBody,
        email: 'qa.seatlock.' + lower + '.' + testId.toLowerCase() + '.' + runStamp + '@example.com',
        dob: autoAdultDob()
    };
}

function resolveDataValue(ctx, value, autoValue) {
    return isAutoToken(value) ? autoValue : String(value || '').trim();
}

function passengerConfig(ctx, userKey) {
    const generated = autoPassengerData(ctx, userKey);
    const fromConfig = (config.passengers && config.passengers[userKey]) || {};

    return {
        name: resolveDataValue(ctx, fromConfig.name, generated.name),
        phone: resolveDataValue(ctx, fromConfig.phone, generated.phone),
        email: resolveDataValue(ctx, fromConfig.email, generated.email),
        dob: resolveDataValue(ctx, fromConfig.dob, generated.dob)
    };
}

function isAutoDateToken(value) {
    const v = normalizeText(value);
    return isAutoToken(value) || v === 'auto_next_available_date' || v === 'auto_next_available' || v === 'next_available' || v === 'today_or_next';
}

function isAutoRouteToken(value) {
    const v = normalizeText(value);
    return isAutoToken(value) || v === 'auto_available_origin' || v === 'auto_available_destination' || v === 'auto_available_route' || v === 'auto_route';
}

function toVietnamDateOnly(value) {
    const d = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(d.getTime())) return '';
    const vietnam = new Date(d.getTime() + 7 * 60 * 60 * 1000);
    return vietnam.toISOString().slice(0, 10);
}

function todayVietnamDateOnly() {
    return toVietnamDateOnly(new Date());
}

function isPastDateOnly(value) {
    const v = String(value || '').trim();
    if (!/^\d{4}-\d{2}-\d{2}$/.test(v)) return false;
    return v < todayVietnamDateOnly();
}

function routeIdCandidates(routeDoc) {
    const raw = routeDoc && (routeDoc._id || routeDoc.id || routeDoc.Id);
    const text = raw ? String(raw) : '';
    const list = [];
    if (text) list.push(text);
    if (text && ObjectId.isValid(text)) list.push(new ObjectId(text));
    return list;
}

function routeNames(routeDoc) {
    return {
        origin: String(routeDoc.departurePoint || routeDoc.DeparturePoint || '').trim(),
        destination: String(routeDoc.destinationPoint || routeDoc.DestinationPoint || '').trim()
    };
}

function routeMatchesRequest(routeDoc, requestedOrigin, requestedDestination) {
    const names = routeNames(routeDoc);
    const dep = normalizeText(names.origin);
    const dest = normalizeText(names.destination);
    const originAuto = isAutoRouteToken(requestedOrigin);
    const destinationAuto = isAutoRouteToken(requestedDestination);
    const originOk = originAuto || dep.includes(normalizeText(requestedOrigin));
    const destOk = destinationAuto || dest.includes(normalizeText(requestedDestination));
    return originOk && destOk && names.origin && names.destination;
}

function countUsableSeatsForAutomation(trip) {
    const seats = trip.realtimeSeats || trip.RealtimeSeats || [];
    if (!seats.length) {
        const fallback = Number(trip.availableSeats ?? trip.AvailableSeats ?? 0);
        return Number.isFinite(fallback) ? fallback : 0;
    }

    return seats.filter(function (seat) {
        const status = normalizeText(seat.status || seat.Status);
        const isBooked = seat.isBooked || seat.IsBooked || status === 'booked' || status === 'sold' || status === 'paid';
        const heldUntil = seat.heldUntil || seat.HeldUntil || seat.holdExpiresAt || seat.HoldExpiresAt;
        const isActiveHold = status === 'holding' && heldUntil && new Date(heldUntil).getTime() > Date.now();
        return !isBooked && !isActiveHold;
    }).length;
}

async function resolveSearchContext(ctx) {
    if (ctx.vars.resolvedSearch && !isPastDateOnly(ctx.vars.resolvedSearch.date)) {
        return ctx.vars.resolvedSearch;
    }

    await connectMongo(ctx);

    const requestedOrigin = String((ctx.current && ctx.current.Origin) || (config.bookingSearch && config.bookingSearch.origin) || '').trim();
    const requestedDestination = String((ctx.current && ctx.current.Destination) || (config.bookingSearch && config.bookingSearch.destination) || '').trim();
    const requestedDateRaw = String((ctx.current && ctx.current.TravelDate) || (config.bookingSearch && config.bookingSearch.date) || '').trim();
    const requestedDate = isAutoDateToken(requestedDateRaw) || isPastDateOnly(requestedDateRaw) ? '' : requestedDateRaw;
    const today = todayVietnamDateOnly();

    const db = getDb(ctx);
    const routesCollection = (config.database && config.database.routesCollection) || 'busroutes';
    const tripsCollection = (config.database && config.database.tripsCollection) || 'trips';

    const allRoutes = await db.collection(routesCollection).find({}).toArray();
    const candidateRoutes = allRoutes.filter(function (routeDoc) {
        return routeMatchesRequest(routeDoc, requestedOrigin, requestedDestination);
    });

    if (!candidateRoutes.length) {
        throw new Error('Khong tim thay route phu hop trong MongoDB. Origin=' + requestedOrigin + ', Destination=' + requestedDestination + '. Neu muon tu dong, de AUTO_AVAILABLE_ORIGIN/AUTO_AVAILABLE_DESTINATION trong Excel hoac config.');
    }

    const statusFilter = { $in: ['Scheduled', 'Active', 'scheduled', 'active', ''] };

    for (let r = 0; r < candidateRoutes.length; r++) {
        const routeDoc = candidateRoutes[r];
        const names = routeNames(routeDoc);
        const ids = routeIdCandidates(routeDoc);
        const routeFilters = [];
        ids.forEach(function (id) {
            routeFilters.push({ routeId: id });
            routeFilters.push({ RouteId: id });
        });
        if (!routeFilters.length) continue;

        const trips = await db.collection(tripsCollection)
            .find({
                $and: [
                    { $or: routeFilters },
                    { $or: [{ status: statusFilter }, { Status: statusFilter }, { status: { $exists: false } }] },
                    { $or: [{ deletedAt: null }, { DeletedAt: null }, { deletedAt: { $exists: false } }] }
                ]
            })
            .sort({ departureTime: 1, DepartureTime: 1 })
            .limit(3000)
            .toArray();

        for (let i = 0; i < trips.length; i++) {
            const trip = trips[i];
            const departure = trip.departureTime || trip.DepartureTime;
            const dateOnly = toVietnamDateOnly(departure);
            if (!dateOnly || dateOnly < today) continue;
            if (requestedDate && dateOnly !== requestedDate) continue;

            const minimumUsableSeats = Math.max(
                2,
                Number((config.bookingSearch && config.bookingSearch.minimumUsableSeats) || 2)
            );
            const usableSeatCount = countUsableSeatsForAutomation(trip);
            if (usableSeatCount < minimumUsableSeats) continue;

            ctx.vars.resolvedSearch = {
                origin: names.origin,
                destination: names.destination,
                date: dateOnly,
                routeId: String(routeDoc._id || routeDoc.id || routeDoc.Id || ''),
                tripIdHint: String(trip._id || trip.id || trip.Id || ''),
                usableSeatCount: usableSeatCount
            };
            console.log('[INFO] Auto search resolved: ' + names.origin + ' -> ' + names.destination + ', date=' + dateOnly + ', trip=' + ctx.vars.resolvedSearch.tripIdHint + ', usableSeats=' + usableSeatCount);
            return ctx.vars.resolvedSearch;
        }
    }

    const requiredSeats = Math.max(
        2,
        Number((config.bookingSearch && config.bookingSearch.minimumUsableSeats) || 2)
    );
    throw new Error('Khong tim thay chuyen tu ngay ' + today + ' tro di co it nhat ' + requiredSeats + ' ghe trong cho dieu kien Origin=' + requestedOrigin + ', Destination=' + requestedDestination + ', TravelDate=' + (requestedDate || 'AUTO') + '. Hay seed lai Trips hoac de Origin/Destination/TravelDate o che do AUTO.');
}

async function resolveTravelDate(ctx) {
    return (await resolveSearchContext(ctx)).date;
}

async function resolveOrigin(ctx) {
    return (await resolveSearchContext(ctx)).origin;
}

async function resolveDestination(ctx) {
    return (await resolveSearchContext(ctx)).destination;
}

function getTcId(ctx) {
    return String((ctx.current && (ctx.current.TestCaseID || ctx.current.ID)) || 'TC');
}

function getHoldCode(ctx, userKey) {
    if (!ctx.holdCodes[userKey]) {
        const safeId = getTcId(ctx).replace(/[^A-Za-z0-9]/g, '').toUpperCase();
        ctx.holdCodes[userKey] = 'QA_' + safeId + '_' + userKey + '_' + Date.now();
    }
    return ctx.holdCodes[userKey];
}

async function createDriver(userKey) {
    const options = new chrome.Options();
    const profileDir = path.join(os.tmpdir(), 'src-travel-seat-lock-' + userKey + '-' + Date.now());

    options.addArguments('--disable-notifications');
    options.addArguments('--disable-popup-blocking');
    options.addArguments('--no-first-run');
    options.addArguments('--no-default-browser-check');
    options.addArguments('--disable-search-engine-choice-screen');
    options.addArguments('--user-data-dir=' + profileDir);

    if (userKey === 'B') {
        options.addArguments('--incognito');
    }

    const driver = await new Builder().forBrowser('chrome').setChromeOptions(options).build();
    await driver.manage().setTimeouts({
        implicit: delay('implicitWaitMs', 1200),
        pageLoad: delay('pageLoadTimeoutMs', 30000),
        script: delay('scriptTimeoutMs', 35000)
    });

    const rect = userKey === 'B'
        ? { width: 940, height: 900, x: 940, y: 0 }
        : { width: 940, height: 900, x: 0, y: 0 };

    await driver.manage().window().setRect(rect).catch(function () {});
    driver.__profileDir = profileDir;
    return driver;
}

function driverOf(ctx, userKey) {
    const driver = ctx.drivers[userKey];
    if (!driver) {
        throw new Error('Chua khoi tao trinh duyet cho ' + userLabel(userKey));
    }
    return driver;
}

async function showStep(driver, text) {
    if (config.execution && config.execution.showStepLabel === false) return;

    await driver.executeScript(function (labelText) {
        let tag = document.getElementById('qa-real-user-step-label');
        if (!tag) {
            tag = document.createElement('div');
            tag.id = 'qa-real-user-step-label';
            tag.style.position = 'fixed';
            tag.style.top = '14px';
            tag.style.left = '50%';
            tag.style.transform = 'translateX(-50%)';
            tag.style.zIndex = '2147483647';
            tag.style.maxWidth = '760px';
            tag.style.padding = '10px 18px';
            tag.style.borderRadius = '999px';
            tag.style.background = '#facc15';
            tag.style.color = '#111827';
            tag.style.fontFamily = 'Arial, sans-serif';
            tag.style.fontSize = '15px';
            tag.style.fontWeight = '900';
            tag.style.boxShadow = '0 14px 35px rgba(0,0,0,.35)';
            tag.style.pointerEvents = 'none';
            document.body.appendChild(tag);
        }
        tag.innerText = labelText;
    }, text).catch(function () {});
}

async function step(ctx, userKey, message, waitMs) {
    const ms = waitMs === undefined ? delay('stepDelayMs', 450) : waitMs;
    const prefix = userKey ? '[' + userLabel(userKey) + '] ' : '';
    console.log('[STEP] ' + prefix + message);
    if (userKey && ctx.drivers[userKey]) {
        await showStep(ctx.drivers[userKey], prefix + message);
    }
    await sleep(ms);
}

async function acceptAlertIfPresent(driver, timeoutMs) {
    const timeout = timeoutMs === undefined ? delay('alertTimeoutMs', 3500) : timeoutMs;
    try {
        await driver.wait(until.alertIsPresent(), timeout);
        const alert = await driver.switchTo().alert();
        const text = await alert.getText().catch(function () { return ''; });
        await sleep(delay('alertReadDelayMs', 500));
        await alert.accept();
        return { found: true, text: text };
    } catch (error) {
        return { found: false, text: '' };
    }
}

async function waitForVisible(driver, selector, timeoutMs) {
    const timeout = timeoutMs === undefined ? delay('elementTimeoutMs', 8000) : timeoutMs;
    const el = await driver.wait(until.elementLocated(By.css(selector)), timeout);
    await driver.wait(until.elementIsVisible(el), timeout);
    return el;
}

async function highlight(driver, element, color) {
    const c = color || '#facc15';
    await driver.executeScript(function (el, highlightColor) {
        if (!el) return;
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        el.style.outline = '4px solid ' + highlightColor;
        el.style.boxShadow = '0 0 0 7px rgba(250, 204, 21, .35)';
        setTimeout(function () {
            el.style.outline = '';
            el.style.boxShadow = '';
        }, 900);
    }, element, c).catch(function () {});
    await sleep(delay('highlightDelayMs', 450));
}

async function hideAutocompleteDropdowns(driver) {
    await driver.executeScript(function () {
        ['dropDeparture', 'dropDestination'].forEach(function (id) {
            const el = document.getElementById(id);
            if (el) {
                el.classList.add('hidden');
                el.style.display = 'none';
                el.style.pointerEvents = 'none';
            }
        });
        const active = document.activeElement;
        if (active && active.blur) active.blur();
    }).catch(function () {});
    await sleep(80);
}

async function setInputValueLikeUser(driver, selector, value) {
    const el = await waitForVisible(driver, selector);
    await highlight(driver, el);

    await driver.executeScript(function (input) {
        input.scrollIntoView({ behavior: 'smooth', block: 'center' });
        input.focus();
        input.value = '';
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }, el);

    const text = String(value || '');
    for (let i = 0; i < text.length; i++) {
        await driver.executeScript(function (input, ch) {
            input.focus();
            input.value = input.value + ch;
            input.dispatchEvent(new Event('input', { bubbles: true }));
        }, el, text[i]);
        await sleep(delay('keyDelayMs', 25));
    }

    await driver.executeScript(function (input) {
        input.dispatchEvent(new Event('change', { bubbles: true }));
        input.blur();
    }, el);
    await hideAutocompleteDropdowns(driver);
    return el;
}

async function setDateInput(driver, selector, value) {
    await hideAutocompleteDropdowns(driver);
    const el = await waitForVisible(driver, selector);
    await highlight(driver, el);
    await driver.executeScript(function (input, inputValue) {
        input.scrollIntoView({ behavior: 'smooth', block: 'center' });
        input.focus();
        input.value = inputValue;
        input.dispatchEvent(new Event('input', { bubbles: true }));
        input.dispatchEvent(new Event('change', { bubbles: true }));
        input.blur();
    }, el, value);
    await hideAutocompleteDropdowns(driver);
    await sleep(delay('stepDelayMs', 450));
    return el;
}

async function clickElement(driver, element, label) {
    if (label) await showStep(driver, label);
    await hideAutocompleteDropdowns(driver);
    await highlight(driver, element);
    await driver.executeScript('arguments[0].scrollIntoView({behavior:"smooth", block:"center"});', element);
    await sleep(180);
    try {
        await element.click();
    } catch (error) {
        await driver.executeScript('arguments[0].click();', element);
    }
}

async function getSearchButton(driver) {
    const element = await driver.executeScript(function () {
        function visible(el) {
            if (!el) return false;
            const style = window.getComputedStyle(el);
            const rect = el.getBoundingClientRect();
            return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
        }
        const exact = document.querySelector('button[onclick*="searchTripsByRoute"]');
        if (visible(exact)) return exact;

        const buttons = Array.from(document.querySelectorAll('button, input[type="button"], input[type="submit"]'));
        return buttons.find(function (btn) {
            if (!visible(btn)) return false;
            const text = String(btn.innerText || btn.value || '').toLowerCase();
            return text.indexOf('tìm kiếm chuyến chạy') >= 0 || text.indexOf('tim kiem chuyen chay') >= 0;
        }) || null;
    });

    if (!element) {
        throw new Error('Khong tim thay nut Tim kiem chuyen chay trong form Booking. Khong duoc bam icon search tren header.');
    }
    return element;
}

async function clickSearchButton(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Bam dung nut Tim kiem chuyen chay trong form');
    const btn = await getSearchButton(driver);
    await clickElement(driver, btn, '[' + userLabel(userKey) + '] Bam nut Tim kiem chuyen chay');
}

async function login(ctx, userKey) {
    if (ctx.loggedIn[userKey]) return;
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Mo trang dang nhap');
    await driver.get(baseUrl() + route('login'));

    const emailSelector = (config.selectors && config.selectors.login && config.selectors.login.email) || '#Email, input[type=email], input[name=Email]';
    const passwordSelector = (config.selectors && config.selectors.login && config.selectors.login.password) || '#Password, input[type=password], input[name=Password]';
    const submitSelector = (config.selectors && config.selectors.login && config.selectors.login.submitBtn) || 'button[type=submit], input[type=submit]';

    const emails = await driver.findElements(By.css(emailSelector));
    const passwords = await driver.findElements(By.css(passwordSelector));
    if (!config.users || !config.users.admin || !config.users.admin.email || !config.users.admin.pass) {
        throw new Error('Thieu tai khoan test users.admin.email/users.admin.pass trong seat-lock-test-data.json');
    }


    if (!emails.length || !passwords.length) {
        console.log('[INFO] ' + userLabel(userKey) + ' co the da dang nhap san. Bo qua buoc login.');
        ctx.loggedIn[userKey] = true;
        return;
    }

    await step(ctx, userKey, 'Nhap email dang nhap');
    await setInputValueLikeUser(driver, emailSelector, (config.users && config.users.admin && config.users.admin.email));

    await step(ctx, userKey, 'Nhap mat khau');
    await setInputValueLikeUser(driver, passwordSelector, (config.users && config.users.admin && config.users.admin.pass));

    await step(ctx, userKey, 'Bam nut dang nhap');
    const submit = await waitForVisible(driver, submitSelector);
    await clickElement(driver, submit, '[' + userLabel(userKey) + '] Bam Dang nhap');
    await sleep(delay('loginWaitMs', 1200));
    ctx.loggedIn[userKey] = true;
}

async function openBooking(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Mo trang Booking/Create');
    await driver.get(baseUrl() + route('bookingCreate'));
    await waitForVisible(driver, '#txtDeparture');
    await setHoldCodeInBrowser(ctx, userKey);
    await hideAutocompleteDropdowns(driver);
}

async function setHoldCodeInBrowser(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    const holdCode = getHoldCode(ctx, userKey);
    await driver.executeScript(function (code) {
        sessionStorage.setItem('SRC_TRAVEL_CURRENT_SEAT_HOLD_CODE', code);
        const input = document.getElementById('seatHoldCode');
        if (input) input.value = code;
        if (typeof saveSeatHoldCode === 'function') {
            try { saveSeatHoldCode(code); } catch (error) {}
        }
    }, holdCode).catch(function () {});
}

async function clearSearch(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Xoa form tim kiem chuyen');
    await driver.executeScript(function () {
        const ids = ['txtDeparture', 'txtDestination', 'txtTravelDate'];
        ids.forEach(function (id) {
            const el = document.getElementById(id);
            if (el) {
                el.value = '';
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
        const detail = document.getElementById('bookingDetailSection');
        if (detail) detail.classList.add('hidden');
    });
    await hideAutocompleteDropdowns(driver);
}

async function setDeparture(ctx, userKey) {
    const origin = await resolveOrigin(ctx);
    await step(ctx, userKey, 'Nhap noi xuat phat: ' + origin);
    await setInputValueLikeUser(driverOf(ctx, userKey), '#txtDeparture', origin);
}

async function setDestination(ctx, userKey) {
    const destination = await resolveDestination(ctx);
    await step(ctx, userKey, 'Nhap noi den: ' + destination);
    await setInputValueLikeUser(driverOf(ctx, userKey), '#txtDestination', destination);
}

async function setTravelDate(ctx, userKey) {
    const date = await resolveTravelDate(ctx);
    await step(ctx, userKey, 'Chon ngay di: ' + date);
    await setDateInput(driverOf(ctx, userKey), '#txtTravelDate', date);
}

async function clearField(ctx, userKey, selector, label) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Xoa truong ' + label);
    await driver.executeScript(function (sel) {
        const el = document.querySelector(sel);
        if (el) {
            el.value = '';
            el.dispatchEvent(new Event('input', { bubbles: true }));
            el.dispatchEvent(new Event('change', { bubbles: true }));
        }
    }, selector);
    await hideAutocompleteDropdowns(driver);
}

async function fillSearch(ctx, userKey) {
    await setDeparture(ctx, userKey);
    await setDestination(ctx, userKey);
    await setTravelDate(ctx, userKey);
}

async function clickSearchEmpty(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await clickSearchButton(ctx, userKey);
    let alert = await acceptAlertIfPresent(driver, 3500);

    if (!alert.found) {
        const calledDirectly = await driver.executeScript(function () {
            if (typeof searchTripsByRoute === 'function') {
                searchTripsByRoute();
                return true;
            }
            return false;
        }).catch(function () { return false; });
        if (calledDirectly) {
            alert = await acceptAlertIfPresent(driver, 3500);
        }
    }

    ctx.lastAlert[userKey] = alert.text;
    console.log('[INFO] Alert ' + userLabel(userKey) + ': ' + (alert.text || '(khong co alert)'));
}

async function assertAlertContains(ctx, userKey) {
    const expected = ctx.current.ExpectedText || ctx.current.ExpectedAlert || 'Vui long';
    const actual = ctx.lastAlert[userKey] || '';
    if (!normalizeText(actual).includes(normalizeText(expected))) {
        throw new Error('Alert khong dung. Mong doi co "' + expected + '", thuc te="' + actual + '"');
    }
}

async function clickSearch(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await clickSearchButton(ctx, userKey);
    await sleep(delay('searchWaitMs', 1500));

    const cardCount = await driver.executeScript(function () {
        return document.querySelectorAll('#searchResultArea > div').length;
    }).catch(function () { return 0; });

    if (!cardCount) {
        await driver.executeAsyncScript(function (done) {
            if (typeof searchTripsByRoute !== 'function') {
                done(false);
                return;
            }
            Promise.resolve(searchTripsByRoute()).then(function () {
                setTimeout(function () { done(true); }, 700);
            }).catch(function () { done(false); });
        }).catch(function () { return false; });
    }
    await sleep(delay('searchWaitMs', 1500));
}

async function assertSearchFormVisible(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Kiem tra form tim kiem hien thi');
    await waitForVisible(driver, '#txtDeparture');
    await waitForVisible(driver, '#txtDestination');
    await waitForVisible(driver, '#txtTravelDate');
    await getSearchButton(driver);
}

async function assertBookingDetailHidden(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Kiem tra khu vuc dat ve dang an');
    const hidden = await driver.executeScript(function () {
        const el = document.getElementById('bookingDetailSection');
        if (!el) return true;
        return el.classList.contains('hidden') || window.getComputedStyle(el).display === 'none';
    });
    if (!hidden) throw new Error('Khu vuc dat ve dang hien thi khi chua chon chuyen.');
}

async function assertTripResults(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Kiem tra co ket qua chuyen xe');
    await driver.wait(async function () {
        const count = await driver.executeScript(function () {
            return document.querySelectorAll('#searchResultArea > div').length;
        });
        return Number(count) > 0;
    }, delay('searchTimeoutMs', 10000));

    const count = await driver.executeScript(function () {
        return document.querySelectorAll('#searchResultArea > div').length;
    });
    if (count <= 0) throw new Error('Khong co chuyen xe nao hien thi sau khi tim kiem.');
    console.log('[INFO] Tim thay ' + count + ' chuyen xe.');
}

async function selectSort(ctx, userKey, sortValue) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Chon sap xep: ' + sortValue);
    const radio = await waitForVisible(driver, 'input[name="tripSort"][value="' + sortValue + '"]');
    await driver.executeScript(function (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        el.click();
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }, radio);
    await sleep(delay('stepDelayMs', 450));
}

async function assertSortSelected(ctx, userKey, sortValue) {
    const driver = driverOf(ctx, userKey);
    const checked = await driver.executeScript(function (value) {
        const el = document.querySelector('input[name="tripSort"][value="' + value + '"]');
        return !!el && el.checked;
    }, sortValue);
    if (!checked) throw new Error('Sap xep ' + sortValue + ' chua duoc chon.');
}

async function selectFirstTrip(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await assertTripResults(ctx, userKey);

    const cards = await driver.findElements(By.css('#searchResultArea > div'));
    if (!cards.length) throw new Error('Khong co card chuyen xe de click.');

    const hintedTripId = String(
        (ctx.vars.resolvedSearch && ctx.vars.resolvedSearch.tripIdHint) || ''
    ).trim();

    let cardIndex = 0;
    if (hintedTripId) {
        cardIndex = await driver.executeAsyncScript(function (targetTripId, done) {
            const dep = document.getElementById('txtDeparture')?.value?.trim() || '';
            const dest = document.getElementById('txtDestination')?.value?.trim() || '';
            const date = document.getElementById('txtTravelDate')?.value || '';
            const url = `/Booking/SearchTrips?departure=${encodeURIComponent(dep)}&destination=${encodeURIComponent(dest)}&date=${encodeURIComponent(date)}`;

            fetch(url)
                .then(function (response) {
                    if (!response.ok) throw new Error('SearchTrips HTTP ' + response.status);
                    return response.json();
                })
                .then(function (trips) {
                    const index = trips.findIndex(function (trip) {
                        return String(trip.id || trip._id || trip.Id || '') === String(targetTripId);
                    });
                    done(index);
                })
                .catch(function () { done(-1); });
        }, hintedTripId);

        if (cardIndex < 0 || cardIndex >= cards.length) {
            throw new Error('MongoDB da resolve trip=' + hintedTripId + ' nhung UI khong co card tuong ung.');
        }
    }

    await step(ctx, userKey, 'Click chon chuyen xe da resolve: index=' + cardIndex);
    await clickElement(driver, cards[cardIndex], '[' + userLabel(userKey) + '] Chon chuyen da resolve');
    await driver.wait(until.elementLocated(By.css('#grid-seats button')), delay('seatMapTimeoutMs', 12000));
    await sleep(delay('seatMapLoadWaitMs', 900));
    await setHoldCodeInBrowser(ctx, userKey);

    const tripId = await driver.executeScript(function () {
        const el = document.getElementById('hidTripId');
        return el ? el.value : '';
    });

    if (hintedTripId && String(tripId) !== hintedTripId) {
        throw new Error('UI chon sai trip. MongoDB resolved=' + hintedTripId + ', UI selected=' + tripId);
    }

    if (tripId) ctx.vars.tripId = tripId;
    console.log('[INFO] TripId hien tai: ' + tripId);
}

async function assertBookingDetailVisible(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    const visible = await driver.executeScript(function () {
        const el = document.getElementById('bookingDetailSection');
        if (!el) return false;
        const style = window.getComputedStyle(el);
        return !el.classList.contains('hidden') && style.display !== 'none';
    });
    if (!visible) throw new Error('Khu vuc thong tin dat ve chua hien thi.');
}

async function getSeats(driver) {
    return await driver.executeScript(function () {
        function cleanSeatText(value) {
            return String(value || '').replace(/[✓✔⏳]/g, '').replace(/\s+/g, '').trim();
        }
        return Array.from(document.querySelectorAll('#grid-seats button')).map(function (btn) {
            const raw = btn.innerText || '';
            const seatNumber = cleanSeatText(raw);
            const cls = String(btn.className || '');
            const title = btn.title || '';
            return {
                seatNumber: seatNumber,
                raw: raw,
                disabled: btn.disabled === true || cls.indexOf('cursor-not-allowed') >= 0 || cls.indexOf('opacity-45') >= 0,
                title: title,
                selected: raw.indexOf('✓') >= 0 || cls.indexOf('bg-amber-500') >= 0,
                holding: raw.indexOf('⏳') >= 0 || /giu|giữ|holding/i.test(title),
                booked: /da duoc dat|đã được đặt|booked/i.test(title)
            };
        }).filter(function (x) { return !!x.seatNumber; });
    });
}

async function findSeatButton(driver, seatNumber) {
    return await driver.executeScript(function (targetSeat) {
        function clean(value) {
            return String(value || '').replace(/[✓✔⏳]/g, '').replace(/\s+/g, '').trim().toUpperCase();
        }
        return Array.from(document.querySelectorAll('#grid-seats button'))
            .find(function (button) { return clean(button.innerText) === String(targetSeat || '').toUpperCase(); }) || null;
    }, seatNumber);
}

async function assertSeatGridVisible(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    const seats = await getSeats(driver);
    if (!seats.length) throw new Error('So do ghe chua hien thi ghe nao.');
    console.log('[INFO] So do ghe hien thi ' + seats.length + ' ghe.');
}

async function assertSeatGridTemplateClasses(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Kiem tra class UI cua cac ghe theo template hien tai');
    const result = await driver.executeScript(function () {
        const buttons = Array.from(document.querySelectorAll('#grid-seats button'));
        if (!buttons.length) return { ok: false, message: 'Khong co button ghe.' };
        const invalid = buttons.filter(function (btn) {
            const cls = String(btn.className || '');
            return cls.indexOf('rounded') < 0 || cls.indexOf('border') < 0 || cls.indexOf('font-bold') < 0;
        });
        return { ok: invalid.length === 0, message: invalid.length + ' ghe thieu class template.' };
    });
    if (!result.ok) throw new Error(result.message);
}

async function fillPassenger(ctx, userKey) {
    const p = passengerConfig(ctx, userKey);
    const suffix = userKey === 'B' ? 'B' : 'A';
    const generated = autoPassengerData(ctx, userKey);
    const name = resolveDataValue(ctx, ctx.current['PassengerName' + suffix] || ctx.current.PassengerName, p.name || generated.name);
    const phone = resolveDataValue(ctx, ctx.current['PassengerPhone' + suffix] || ctx.current.PassengerPhone, p.phone || generated.phone);
    const email = resolveDataValue(ctx, ctx.current['PassengerEmail' + suffix] || ctx.current.PassengerEmail, p.email || generated.email);
    const dob = resolveDataValue(ctx, ctx.current['Dob' + suffix] || ctx.current.DOB, p.dob || generated.dob);

    await step(ctx, userKey, 'Nhap so dien thoai khach: ' + phone);
    await setInputValueLikeUser(driverOf(ctx, userKey), '#txtPassengerPhone', phone);

    await step(ctx, userKey, 'Nhap ten hanh khach: ' + name);
    await setInputValueLikeUser(driverOf(ctx, userKey), '#txtPassengerName', name);

    await step(ctx, userKey, 'Nhap ngay sinh: ' + dob);
    await setDateInput(driverOf(ctx, userKey), '#inputDob', dob);

    await step(ctx, userKey, 'Nhap email: ' + email);
    await setInputValueLikeUser(driverOf(ctx, userKey), '#txtPassengerEmail', email);
}

async function clearPassenger(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Xoa thong tin khach hang');
    await driver.executeScript(function () {
        ['txtPassengerPhone', 'txtPassengerName', 'inputDob', 'txtPassengerEmail'].forEach(function (id) {
            const el = document.getElementById(id);
            if (el) {
                el.value = '';
                el.dispatchEvent(new Event('input', { bubbles: true }));
                el.dispatchEvent(new Event('change', { bubbles: true }));
            }
        });
    });
}

async function assertPassengerValues(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    const ok = await driver.executeScript(function () {
        const ids = ['txtPassengerPhone', 'txtPassengerName', 'inputDob'];
        return ids.every(function (id) {
            const el = document.getElementById(id);
            return el && String(el.value || '').trim().length > 0;
        });
    });
    if (!ok) throw new Error('Thong tin khach hang chua duoc nhap day du.');
}

async function chooseSeat(ctx, userKey, mode) {
    const driver = driverOf(ctx, userKey);
    await setHoldCodeInBrowser(ctx, userKey);
    const seats = await getSeats(driver);
    if (!seats.length) throw new Error('Chua co so do ghe de chon.');

    let seatNumber = '';
    if (mode === 'SAME_AS_A') {
        seatNumber = ctx.vars.ASeat;
    } else if (mode === 'SAME_AS_B') {
        seatNumber = ctx.vars.BSeat;
    } else if (mode === 'SECOND_AVAILABLE_EXCEPT_A') {
        seatNumber = (seats.find(function (s) { return !s.disabled && !s.selected && s.seatNumber !== ctx.vars.ASeat; }) || {}).seatNumber || '';
    } else if (mode === 'SECOND_AVAILABLE') {
        const available = seats.filter(function (s) { return !s.disabled && !s.selected; });
        seatNumber = (available[1] || available[0] || {}).seatNumber || '';
    } else if (mode === 'TARGET') {
        seatNumber = ctx.current.TargetSeat || ctx.vars.TargetSeat;
    } else {
        seatNumber = (seats.find(function (s) { return !s.disabled && !s.selected; }) || {}).seatNumber || '';
    }

    if (!seatNumber) throw new Error('Khong tim thay ghe phu hop de chon. mode=' + mode);

    const btn = await findSeatButton(driver, seatNumber);
    if (!btn) throw new Error('Khong tim thay button ghe ' + seatNumber);

    const disabled = await btn.getAttribute('disabled');
    if (disabled) throw new Error('Ghe ' + seatNumber + ' dang bi khoa, khong the click nhu ghe trong.');

    await step(ctx, userKey, 'Click chon ghe ' + seatNumber);
    await clickElement(driver, btn, '[' + userLabel(userKey) + '] Chon ghe ' + seatNumber);
    await sleep(delay('seatHoldWaitMs', 1200));

    const alert = await acceptAlertIfPresent(driver, 1800);
    if (alert.found) throw new Error('Khong giu duoc ghe ' + seatNumber + '. Alert: ' + alert.text);

    if (userKey === 'A') ctx.vars.ASeat = seatNumber;
    if (userKey === 'B') ctx.vars.BSeat = seatNumber;
    ctx.vars.LastSeat = seatNumber;
    console.log('[INFO] ' + userLabel(userKey) + ' dang giu ghe: ' + seatNumber);
}

async function holdFirstAvailable(ctx, userKey) { await chooseSeat(ctx, userKey, 'FIRST_AVAILABLE'); }
async function holdSecondAvailable(ctx, userKey) { await chooseSeat(ctx, userKey, 'SECOND_AVAILABLE'); }
async function holdSecondAvailableExceptA(ctx, userKey) { await chooseSeat(ctx, userKey, 'SECOND_AVAILABLE_EXCEPT_A'); }

async function assertOwnSeatSelected(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    const seatNumber = userKey === 'A' ? ctx.vars.ASeat : ctx.vars.BSeat;
    if (!seatNumber) throw new Error('Khong co bien ghe cua ' + userLabel(userKey) + ' de kiem tra.');

    await step(ctx, userKey, 'Kiem tra ghe ' + seatNumber + ' van la ghe cua chinh minh');
    const seats = await getSeats(driver);
    const seat = seats.find(function (s) { return s.seatNumber === seatNumber; });
    if (!seat) throw new Error('Khong thay ghe ' + seatNumber + ' tren so do.');
    if (!seat.selected) {
        throw new Error('Ghe ' + seatNumber + ' khong con hien thi la dang chon cua ' + userLabel(userKey) + '. raw=' + seat.raw + ', title=' + seat.title);
    }
    if (seat.disabled) {
        throw new Error('Ghe ' + seatNumber + ' la ghe cua ' + userLabel(userKey) + ' nhung bi disabled.');
    }
}

async function assertSelectedTextHasSeat(ctx, userKey, owner) {
    const driver = driverOf(ctx, userKey);
    const seatNumber = owner === 'B' ? ctx.vars.BSeat : ctx.vars.ASeat;
    const text = await driver.executeScript(function () {
        const el = document.getElementById('txtSeatNum');
        return el ? el.innerText : '';
    });
    if (!String(text || '').includes(seatNumber)) {
        throw new Error('O Ghe da chon chua hien dung ghe ' + seatNumber + '. Text=' + text);
    }
}

async function assertTimerVisible(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    const visible = await driver.executeScript(function () {
        const box = document.getElementById('seatHoldTimerText');
        const count = document.getElementById('seatHoldCountdown');
        if (!box || !count) return false;
        const style = window.getComputedStyle(box);
        return !box.classList.contains('hidden') && style.display !== 'none' && String(count.innerText || '').trim().length > 0;
    });
    if (!visible) throw new Error('Dong dem nguoc giu cho chua hien thi.');
}

async function assertSeatLockedFor(ctx, userKey, seatOwner) {
    const driver = driverOf(ctx, userKey);
    const seatNumber = seatOwner === 'A' ? ctx.vars.ASeat : ctx.vars.BSeat;
    if (!seatNumber) throw new Error('Khong co bien ghe cua User ' + seatOwner + '.');

    await step(ctx, userKey, 'Kiem tra ghe ' + seatNumber + ' dang bi khoa voi ' + userLabel(userKey));
    await refreshSeatMap(ctx, userKey);
    const seats = await getSeats(driver);
    const seat = seats.find(function (s) { return s.seatNumber === seatNumber; });
    if (!seat) throw new Error('Khong thay ghe ' + seatNumber + ' tren so do.');

    if (!seat.disabled && !seat.holding && !/nguoi khac|người khác|giu|giữ|holding/i.test(seat.title)) {
        throw new Error('Ghe ' + seatNumber + ' chua bi khoa voi ' + userLabel(userKey) + '. raw=' + seat.raw + ', title=' + seat.title + ', disabled=' + seat.disabled);
    }
}

async function tryClickSeatExpectBlocked(ctx, userKey, seatOwner) {
    const driver = driverOf(ctx, userKey);
    const seatNumber = seatOwner === 'A' ? ctx.vars.ASeat : ctx.vars.BSeat;
    if (!seatNumber) throw new Error('Khong co ghe cua User ' + seatOwner + ' de thu click.');

    await step(ctx, userKey, 'Thu click ghe ' + seatNumber + ' dang do User ' + seatOwner + ' giu, ky vong bi chan');
    const before = (await getSeats(driver)).find(function (s) { return s.seatNumber === seatNumber; });
    if (!before) throw new Error('Khong thay ghe ' + seatNumber + '.');

    if (before.disabled) {
        console.log('[INFO] Ghe ' + seatNumber + ' dang disabled, dung ky vong.');
        return;
    }

    const btn = await findSeatButton(driver, seatNumber);
    await clickElement(driver, btn, '[' + userLabel(userKey) + '] Thu click ghe bi giu ' + seatNumber);
    await sleep(delay('seatHoldWaitMs', 1200));
    const alert = await acceptAlertIfPresent(driver, 2500);
    ctx.lastAlert[userKey] = alert.text;

    const after = (await getSeats(driver)).find(function (s) { return s.seatNumber === seatNumber; });
    if (after && after.selected) {
        throw new Error(userLabel(userKey) + ' da chon duoc ghe ' + seatNumber + ' cua User ' + seatOwner + ', sai logic khoa ghe.');
    }
    console.log('[INFO] Ket qua bi chan hop le. Alert=' + (alert.text || '(disabled hoac khong co alert)'));
}

async function refreshSeatMap(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await setHoldCodeInBrowser(ctx, userKey);
    await step(ctx, userKey, 'Tai lai so do ghe cua chuyen hien tai');
    const ok = await driver.executeAsyncScript(function (done) {
        const tripId = document.getElementById('hidTripId') ? document.getElementById('hidTripId').value : '';
        if (!tripId || typeof loadSeatMatrix !== 'function') {
            done(false);
            return;
        }
        Promise.resolve(loadSeatMatrix(tripId)).then(function () { done(true); }).catch(function () { done(false); });
    }).catch(function () { return false; });
    if (!ok) {
        console.log('[INFO] Khong goi duoc loadSeatMatrix, thuc hien mo lai chuyen.');
        await reopenSameTrip(ctx, userKey);
        return;
    }
    await sleep(delay('seatMapLoadWaitMs', 900));
}

async function releaseAll(ctx, userKey) {
    const driver = ctx.drivers[userKey];
    if (!driver) return;
    await step(ctx, userKey, 'Bo chon toan bo ghe dang giu de cleanup', 150);

    for (let i = 0; i < 8; i++) {
        const selected = (await getSeats(driver).catch(function () { return []; })).filter(function (s) { return s.selected; });
        if (!selected.length) break;
        const btn = await findSeatButton(driver, selected[0].seatNumber);
        if (!btn) break;
        await clickElement(driver, btn, '[' + userLabel(userKey) + '] Bo chon ghe ' + selected[0].seatNumber);
        await sleep(delay('seatReleaseWaitMs', 700));
        await acceptAlertIfPresent(driver, 1200);
    }
}

async function assertSeatAvailable(ctx, userKey, seatSource) {
    const driver = driverOf(ctx, userKey);
    const seatNumber = seatSource === 'A' ? ctx.vars.ASeat : seatSource === 'B' ? ctx.vars.BSeat : ctx.vars.LastSeat;
    if (!seatNumber) throw new Error('Khong co seatNumber de kiem tra Available.');

    await step(ctx, userKey, 'Kiem tra ghe ' + seatNumber + ' da duoc nha va co the chon lai');
    const seats = await getSeats(driver);
    const seat = seats.find(function (s) { return s.seatNumber === seatNumber; });
    if (!seat) throw new Error('Khong thay ghe ' + seatNumber + '.');
    if (seat.disabled || seat.selected || seat.holding) {
        throw new Error('Ghe ' + seatNumber + ' chua Available. raw=' + seat.raw + ', disabled=' + seat.disabled + ', title=' + seat.title);
    }
}

async function submitBookingExpectNoSeat(ctx, userKey) {
    const driver = driverOf(ctx, userKey);
    await step(ctx, userKey, 'Bam Xac nhan dat ve khi chua chon ghe de kiem tra validation');
    const submit = await waitForVisible(driver, '#formBooking button[type=submit]');
    await clickElement(driver, submit, '[' + userLabel(userKey) + '] Bam Xac nhan dat ve');
    const alert = await acceptAlertIfPresent(driver, 3500);
    ctx.lastAlert[userKey] = alert.text;
    const text = normalizeText(alert.text);
    if (!text.includes('chon') && !text.includes('cho ngoi') && !text.includes('chỗ ngồi')) {
        throw new Error('Khong thay alert validation chua chon ghe. Alert="' + alert.text + '"');
    }
}

async function findFirstVisiblePaymentValue(driver) {
    return await driver.executeScript(function () {
        function visible(el) {
            if (!el) return false;
            const style = window.getComputedStyle(el);
            const rect = el.getBoundingClientRect();
            return style.display !== 'none' && style.visibility !== 'hidden' && rect.width > 0 && rect.height > 0;
        }
        const radios = Array.from(document.querySelectorAll('input[name="paymentMethod"]'));
        const target = radios.find(function (radio) {
            if (visible(radio)) return true;
            const label = radio.closest('label') || document.querySelector('label[for="' + radio.id + '"]');
            return visible(label);
        });
        return target ? String(target.value || '') : '';
    });
}

async function resolvePaymentValue(ctx, userKey, paymentMethod) {
    const raw = paymentMethod || ctx.current.PaymentMethod || (config.booking && config.booking.paymentMethod) || '';
    if (!isAutoToken(raw) && normalizeText(raw) !== 'auto_first_available_payment_method') {
        return String(raw).trim();
    }
    const value = await findFirstVisiblePaymentValue(driverOf(ctx, userKey));
    if (!value) throw new Error('Khong tim thay phuong thuc thanh toan nao tren UI.');
    return value;
}

async function selectPayment(ctx, userKey, paymentMethod) {
    const driver = driverOf(ctx, userKey);
    const value = await resolvePaymentValue(ctx, userKey, paymentMethod);
    ctx.vars['Payment' + userKey] = value;
    await step(ctx, userKey, 'Chon phuong thuc thanh toan: ' + value);
    const radio = await waitForVisible(driver, 'input[name="paymentMethod"][value="' + value + '"]');
    await driver.executeScript(function (el) {
        el.scrollIntoView({ behavior: 'smooth', block: 'center' });
        el.click();
        el.dispatchEvent(new Event('change', { bubbles: true }));
    }, radio);
    await sleep(delay('stepDelayMs', 450));
}

async function assertPaymentSelected(ctx, userKey, paymentMethod) {
    const driver = driverOf(ctx, userKey);
    const value = paymentMethod || ctx.vars['Payment' + userKey] || await resolvePaymentValue(ctx, userKey, paymentMethod);
    const checked = await driver.executeScript(function (paymentValue) {
        const el = document.querySelector('input[name="paymentMethod"][value="' + paymentValue + '"]');
        return !!el && el.checked;
    }, value);
    if (!checked) throw new Error('Phuong thuc thanh toan ' + value + ' chua duoc chon.');
}

async function waitHoldTimeout(ctx, userKey) {
    const seconds = Number(ctx.current.TimeoutSeconds || (config.execution && config.execution.holdTimeoutSeconds) || 185);
    await step(ctx, userKey, 'Cho het thoi gian giu cho ' + seconds + ' giay', 200);
    let remain = seconds;
    while (remain > 0) {
        const chunk = Math.min(15, remain);
        console.log('[INFO] Con khoang ' + remain + ' giay.');
        await sleep(chunk * 1000);
        await acceptAlertIfPresent(driverOf(ctx, userKey), 500).catch(function () {});
        remain -= chunk;
    }
    await sleep(1200);
    await acceptAlertIfPresent(driverOf(ctx, userKey), 1500).catch(function () {});
}

async function prepareBooking(ctx, userKey) {
    await openBooking(ctx, userKey);
    await fillSearch(ctx, userKey);
    await clickSearch(ctx, userKey);
    await selectFirstTrip(ctx, userKey);
    await fillPassenger(ctx, userKey);
}

async function reopenSameTrip(ctx, userKey) {
    await openBooking(ctx, userKey);
    await fillSearch(ctx, userKey);
    await clickSearch(ctx, userKey);
    await selectFirstTrip(ctx, userKey);
}

async function setupAHoldsSeat(ctx) {
    await prepareBooking(ctx, 'A');
    await holdFirstAvailable(ctx, 'A');
    await assertOwnSeatSelected(ctx, 'A');
}

async function setupABHoldDifferent(ctx) {
    await prepareBooking(ctx, 'A');
    await holdFirstAvailable(ctx, 'A');
    await prepareBooking(ctx, 'B');
    await assertSeatLockedFor(ctx, 'B', 'A');
    await holdSecondAvailableExceptA(ctx, 'B');
    await assertOwnSeatSelected(ctx, 'B');
}

async function concurrentSameSeatExpectOneBlocked(ctx) {
    await prepareBooking(ctx, 'A');
    await prepareBooking(ctx, 'B');

    const aSeats = await getSeats(driverOf(ctx, 'A'));
    const target = (aSeats.find(function (s) { return !s.disabled && !s.selected; }) || {}).seatNumber;
    if (!target) throw new Error('Khong tim thay ghe trong de test concurrency.');
    ctx.vars.ASeat = target;
    ctx.vars.BSeat = target;
    ctx.vars.LastSeat = target;

    const aBtn = await findSeatButton(driverOf(ctx, 'A'), target);
    const bBtn = await findSeatButton(driverOf(ctx, 'B'), target);
    if (!aBtn || !bBtn) throw new Error('Khong tim thay ghe target ' + target + ' tren ca 2 browser.');

    await step(ctx, null, 'Mo phong 2 user click cung ghe ' + target + ' gan cung luc', 200);
    const p1 = clickElement(driverOf(ctx, 'A'), aBtn, '[User A] Click cung ghe ' + target).catch(function (error) { return error; });
    const p2 = clickElement(driverOf(ctx, 'B'), bBtn, '[User B] Click cung ghe ' + target).catch(function (error) { return error; });
    await Promise.all([p1, p2]);
    await sleep(delay('seatHoldWaitMs', 1400));

    await acceptAlertIfPresent(driverOf(ctx, 'A'), 1200);
    await acceptAlertIfPresent(driverOf(ctx, 'B'), 1200);
    await refreshSeatMap(ctx, 'A').catch(function () {});
    await refreshSeatMap(ctx, 'B').catch(function () {});

    const aSeat = (await getSeats(driverOf(ctx, 'A'))).find(function (s) { return s.seatNumber === target; });
    const bSeat = (await getSeats(driverOf(ctx, 'B'))).find(function (s) { return s.seatNumber === target; });
    const aSelected = aSeat && aSeat.selected;
    const bSelected = bSeat && bSeat.selected;

    if (aSelected && bSelected) {
        throw new Error('Ca User A va User B deu giu duoc cung ghe ' + target + '. Co nguy co overbooking.');
    }
    if (!aSelected && !bSelected) {
        throw new Error('Khong user nao giu duoc ghe trong concurrency test.');
    }
    console.log('[INFO] Concurrency hop le. A selected=' + aSelected + ', B selected=' + bSelected);
}

function getDb(ctx) {
    if (!ctx.db) throw new Error('Chua ket noi MongoDB.');
    return ctx.db;
}

function tripObjectIdOrString(tripId) {
    return ObjectId.isValid(tripId) ? new ObjectId(tripId) : tripId;
}

async function getTripFromDb(ctx) {
    const tripId = ctx.vars.tripId;
    if (!tripId) throw new Error('Khong co tripId de kiem tra MongoDB.');
    const tripsCollection = (config.database && config.database.tripsCollection) || 'trips';
    return await getDb(ctx).collection(tripsCollection).findOne({
        $or: [
            { _id: tripObjectIdOrString(tripId) },
            { _id: tripId },
            { id: tripId },
            { Id: tripId }
        ]
    });
}

function findDbSeat(trip, seatNumber) {
    const list = (trip && (trip.realtimeSeats || trip.RealtimeSeats)) || [];
    return list.find(function (seat) {
        const sn = seat.seatNumber || seat.SeatNumber;
        return String(sn || '').trim().toUpperCase() === String(seatNumber || '').trim().toUpperCase();
    }) || null;
}

async function mongoAssertSeatHolding(ctx, owner) {
    const seatNumber = owner === 'B' ? ctx.vars.BSeat : ctx.vars.ASeat;
    const holdCode = getHoldCode(ctx, owner);
    if (!seatNumber) throw new Error('Khong co ghe User ' + owner + ' de kiem tra MongoDB.');

    const trip = await getTripFromDb(ctx);
    const seat = findDbSeat(trip, seatNumber);
    if (!seat) throw new Error('MongoDB khong co realtimeSeats cho ghe ' + seatNumber + '.');

    const status = seat.status || seat.Status;
    const heldBy = seat.heldByCustomerId || seat.HeldByCustomerId;
    const heldUntil = seat.heldUntil || seat.HeldUntil;
    if (status !== 'Holding') throw new Error('MongoDB ghe ' + seatNumber + ' status khong phai Holding. Status=' + status);
    if (heldBy !== holdCode) throw new Error('MongoDB ghe ' + seatNumber + ' heldBy bi sai. Expected=' + holdCode + ', actual=' + heldBy);
    if (heldUntil && new Date(heldUntil).getTime() <= Date.now()) throw new Error('MongoDB ghe ' + seatNumber + ' da het han giu cho.');
}

async function mongoAssertSeatAvailable(ctx, owner) {
    const seatNumber = owner === 'B' ? ctx.vars.BSeat : ctx.vars.ASeat;
    if (!seatNumber) throw new Error('Khong co ghe User ' + owner + ' de kiem tra Available.');
    const trip = await getTripFromDb(ctx);
    const seat = findDbSeat(trip, seatNumber);
    if (!seat) return;
    const status = seat.status || seat.Status || '';
    if (status === 'Holding' || status === 'Booked') {
        throw new Error('MongoDB ghe ' + seatNumber + ' chua duoc nha. Status=' + status);
    }
}

async function mongoAssertNoOverbooking(ctx, owner) {
    const seatNumber = owner === 'B' ? ctx.vars.BSeat : ctx.vars.ASeat;
    const tripId = ctx.vars.tripId;
    if (!seatNumber || !tripId) throw new Error('Thieu tripId hoac seatNumber de quet duplicate booking.');
    const bookingsCollection = (config.database && config.database.bookingsCollection) || 'bookings';
    const bookings = await getDb(ctx).collection(bookingsCollection).find({
        $or: [
            { tripId: { $eq: tripId }, 'passengers.seatNumber': { $eq: seatNumber } },
            { TripId: { $eq: tripId }, 'Passengers.SeatNumber': { $eq: seatNumber } },
            { tripId: { $eq: tripObjectIdOrString(tripId) }, 'passengers.seatNumber': { $eq: seatNumber } },
            { TripId: { $eq: tripObjectIdOrString(tripId) }, 'Passengers.SeatNumber': { $eq: seatNumber } }
        ]
    }).toArray();

    const valid = bookings.filter(function (booking) {
        const bookingStatus = normalizeText(booking.bookingStatus || booking.BookingStatus || booking.status || booking.Status);
        const paymentStatus = normalizeText(booking.paymentStatus || booking.PaymentStatus);
        return !bookingStatus.includes('cancel') && !bookingStatus.includes('refund') && !paymentStatus.includes('refund');
    });

    if (valid.length > 1) {
        throw new Error('Phat hien overbooking ghe ' + seatNumber + '. So booking hop le=' + valid.length);
    }
    console.log('[INFO] MongoDB no-overbooking OK. Seat=' + seatNumber + ', validBookingCount=' + valid.length);
}

async function resetHoldingSeatsByFilter(ctx, matchFilter, arrayFilter) {
    const tripsCollection = (config.database && config.database.tripsCollection) || 'trips';
    await getDb(ctx).collection(tripsCollection).updateMany(
        matchFilter,
        {
            $set: {
                'realtimeSeats.$[seat].status': 'Available',
                'realtimeSeats.$[seat].Status': 'Available',
                'realtimeSeats.$[seat].heldUntil': null,
                'realtimeSeats.$[seat].HeldUntil': null,
                'realtimeSeats.$[seat].heldByCustomerId': null,
                'realtimeSeats.$[seat].HeldByCustomerId': null,
                'realtimeSeats.$[seat].isHolding': false,
                'realtimeSeats.$[seat].IsHolding': false,
                'realtimeSeats.$[seat].isLocked': false,
                'realtimeSeats.$[seat].IsLocked': false
            }
        },
        { arrayFilters: [arrayFilter] }
    );
}

async function mongoCleanupHolds(ctx) {
    if (!ctx.db) return;
    const codes = Object.values(ctx.holdCodes || {}).filter(Boolean);
    if (!codes.length) return;
    await resetHoldingSeatsByFilter(
        ctx,
        { $or: [
            { 'realtimeSeats.heldByCustomerId': { $in: codes } },
            { 'realtimeSeats.HeldByCustomerId': { $in: codes } }
        ] },
        { $or: [
            { 'seat.heldByCustomerId': { $in: codes } },
            { 'seat.HeldByCustomerId': { $in: codes } }
        ] }
    );
}

async function mongoCleanupAllQaHolds(ctx) {
    if (!ctx.db) return;
    await resetHoldingSeatsByFilter(
        ctx,
        { $or: [
            { 'realtimeSeats.heldByCustomerId': { $regex: '^QA_' } },
            { 'realtimeSeats.HeldByCustomerId': { $regex: '^QA_' } }
        ] },
        { $or: [
            { 'seat.heldByCustomerId': { $regex: '^QA_' } },
            { 'seat.HeldByCustomerId': { $regex: '^QA_' } }
        ] }
    );
}

async function connectMongo(ctx) {
    if (ctx.db) return;
    const uri = config.database && config.database.uri;
    const dbName = config.database && config.database.dbName;
    if (!uri || !dbName) {
        throw new Error('Thieu MongoDB config. Can co database.uri va database.dbName trong seat-lock-test-data.json');
    }
    ctx.mongoClient = new MongoClient(uri);
    await ctx.mongoClient.connect();
    ctx.db = ctx.mongoClient.db(dbName);
    console.log('[INFO] Connected MongoDB database: ' + dbName);
}

async function assertMongoConnected(ctx) {
    await connectMongo(ctx);
    const collections = await ctx.db.listCollections().toArray();
    if (!collections.length) throw new Error('MongoDB connected nhung khong doc duoc collection nao.');
}

async function assertRouteConfig(ctx) {
    const required = ['login', 'bookingCreate'];
    for (let i = 0; i < required.length; i++) {
        if (!route(required[i], '')) throw new Error('Thieu config.routes.' + required[i]);
    }
}

async function assertExcelRuntime(ctx) {
    if (!Array.isArray(testCases) || !testCases.length) throw new Error('Khong doc duoc testcase tu Excel.');
}

async function assertKeywordMapping(ctx) {
    const missing = [];
    for (let i = 0; i < testCases.length; i++) {
        const flow = splitFlow(testCases[i].ActionFlow);
        for (let j = 0; j < flow.length; j++) {
            if (!keywordHandlers[flow[j]]) missing.push((testCases[i].TestCaseID || 'TC') + ':' + flow[j]);
        }
    }
    if (missing.length) throw new Error('Thieu keyword handler: ' + missing.join(', '));
}

const keywordHandlers = {
    LOGIN_A: function (ctx) { return login(ctx, 'A'); },
    LOGIN_B: function (ctx) { return login(ctx, 'B'); },

    OPEN_BOOKING_A: function (ctx) { return openBooking(ctx, 'A'); },
    OPEN_BOOKING_B: function (ctx) { return openBooking(ctx, 'B'); },
    REOPEN_SAME_TRIP_A: function (ctx) { return reopenSameTrip(ctx, 'A'); },
    REOPEN_SAME_TRIP_B: function (ctx) { return reopenSameTrip(ctx, 'B'); },

    ASSERT_SEARCH_FORM_VISIBLE_A: function (ctx) { return assertSearchFormVisible(ctx, 'A'); },
    ASSERT_SEARCH_FORM_VISIBLE_B: function (ctx) { return assertSearchFormVisible(ctx, 'B'); },
    ASSERT_BOOKING_DETAIL_HIDDEN_A: function (ctx) { return assertBookingDetailHidden(ctx, 'A'); },
    ASSERT_BOOKING_DETAIL_HIDDEN_B: function (ctx) { return assertBookingDetailHidden(ctx, 'B'); },
    ASSERT_BOOKING_DETAIL_VISIBLE_A: function (ctx) { return assertBookingDetailVisible(ctx, 'A'); },
    ASSERT_BOOKING_DETAIL_VISIBLE_B: function (ctx) { return assertBookingDetailVisible(ctx, 'B'); },

    CLEAR_SEARCH_A: function (ctx) { return clearSearch(ctx, 'A'); },
    CLEAR_SEARCH_B: function (ctx) { return clearSearch(ctx, 'B'); },
    SET_DEPARTURE_A: function (ctx) { return setDeparture(ctx, 'A'); },
    SET_DEPARTURE_B: function (ctx) { return setDeparture(ctx, 'B'); },
    SET_DESTINATION_A: function (ctx) { return setDestination(ctx, 'A'); },
    SET_DESTINATION_B: function (ctx) { return setDestination(ctx, 'B'); },
    SET_DATE_A: function (ctx) { return setTravelDate(ctx, 'A'); },
    SET_DATE_B: function (ctx) { return setTravelDate(ctx, 'B'); },
    CLEAR_DEPARTURE_A: function (ctx) { return clearField(ctx, 'A', '#txtDeparture', 'Noi xuat phat'); },
    CLEAR_DESTINATION_A: function (ctx) { return clearField(ctx, 'A', '#txtDestination', 'Noi den'); },
    CLEAR_DATE_A: function (ctx) { return clearField(ctx, 'A', '#txtTravelDate', 'Ngay di'); },
    CLEAR_DEPARTURE_B: function (ctx) { return clearField(ctx, 'B', '#txtDeparture', 'Noi xuat phat'); },
    CLEAR_DESTINATION_B: function (ctx) { return clearField(ctx, 'B', '#txtDestination', 'Noi den'); },
    CLEAR_DATE_B: function (ctx) { return clearField(ctx, 'B', '#txtTravelDate', 'Ngay di'); },

    SEARCH_EMPTY_A: function (ctx) { return clickSearchEmpty(ctx, 'A'); },
    SEARCH_EMPTY_B: function (ctx) { return clickSearchEmpty(ctx, 'B'); },
    ASSERT_ALERT_CONTAINS_A: function (ctx) { return assertAlertContains(ctx, 'A'); },
    ASSERT_ALERT_CONTAINS_B: function (ctx) { return assertAlertContains(ctx, 'B'); },

    FILL_SEARCH_A: function (ctx) { return fillSearch(ctx, 'A'); },
    FILL_SEARCH_B: function (ctx) { return fillSearch(ctx, 'B'); },
    CLICK_SEARCH_A: function (ctx) { return clickSearch(ctx, 'A'); },
    CLICK_SEARCH_B: function (ctx) { return clickSearch(ctx, 'B'); },
    ASSERT_TRIP_RESULTS_A: function (ctx) { return assertTripResults(ctx, 'A'); },
    ASSERT_TRIP_RESULTS_B: function (ctx) { return assertTripResults(ctx, 'B'); },

    SELECT_SORT_DEFAULT_A: function (ctx) { return selectSort(ctx, 'A', 'default'); },
    SELECT_SORT_TIME_ASC_A: function (ctx) { return selectSort(ctx, 'A', 'timeAsc'); },
    SELECT_SORT_PRICE_ASC_A: function (ctx) { return selectSort(ctx, 'A', 'priceAsc'); },
    SELECT_SORT_PRICE_DESC_A: function (ctx) { return selectSort(ctx, 'A', 'priceDesc'); },
    SELECT_SORT_DEFAULT_B: function (ctx) { return selectSort(ctx, 'B', 'default'); },
    SELECT_SORT_TIME_ASC_B: function (ctx) { return selectSort(ctx, 'B', 'timeAsc'); },
    SELECT_SORT_PRICE_ASC_B: function (ctx) { return selectSort(ctx, 'B', 'priceAsc'); },
    SELECT_SORT_PRICE_DESC_B: function (ctx) { return selectSort(ctx, 'B', 'priceDesc'); },
    ASSERT_SORT_DEFAULT_A: function (ctx) { return assertSortSelected(ctx, 'A', 'default'); },
    ASSERT_SORT_TIME_ASC_A: function (ctx) { return assertSortSelected(ctx, 'A', 'timeAsc'); },
    ASSERT_SORT_PRICE_ASC_A: function (ctx) { return assertSortSelected(ctx, 'A', 'priceAsc'); },
    ASSERT_SORT_PRICE_DESC_A: function (ctx) { return assertSortSelected(ctx, 'A', 'priceDesc'); },

    SELECT_FIRST_TRIP_A: function (ctx) { return selectFirstTrip(ctx, 'A'); },
    SELECT_FIRST_TRIP_B: function (ctx) { return selectFirstTrip(ctx, 'B'); },
    ASSERT_SEAT_GRID_VISIBLE_A: function (ctx) { return assertSeatGridVisible(ctx, 'A'); },
    ASSERT_SEAT_GRID_VISIBLE_B: function (ctx) { return assertSeatGridVisible(ctx, 'B'); },
    ASSERT_SEAT_GRID_TEMPLATE_CLASSES_A: function (ctx) { return assertSeatGridTemplateClasses(ctx, 'A'); },
    ASSERT_SEAT_GRID_TEMPLATE_CLASSES_B: function (ctx) { return assertSeatGridTemplateClasses(ctx, 'B'); },

    FILL_PASSENGER_A: function (ctx) { return fillPassenger(ctx, 'A'); },
    FILL_PASSENGER_B: function (ctx) { return fillPassenger(ctx, 'B'); },
    CLEAR_PASSENGER_A: function (ctx) { return clearPassenger(ctx, 'A'); },
    CLEAR_PASSENGER_B: function (ctx) { return clearPassenger(ctx, 'B'); },
    ASSERT_PASSENGER_VALUES_A: function (ctx) { return assertPassengerValues(ctx, 'A'); },
    ASSERT_PASSENGER_VALUES_B: function (ctx) { return assertPassengerValues(ctx, 'B'); },

    HOLD_FIRST_AVAILABLE_A: function (ctx) { return holdFirstAvailable(ctx, 'A'); },
    HOLD_FIRST_AVAILABLE_B: function (ctx) { return holdFirstAvailable(ctx, 'B'); },
    HOLD_SECOND_AVAILABLE_A: function (ctx) { return holdSecondAvailable(ctx, 'A'); },
    HOLD_SECOND_AVAILABLE_B: function (ctx) { return holdSecondAvailable(ctx, 'B'); },
    HOLD_DIFFERENT_FROM_A_B: function (ctx) { return holdSecondAvailableExceptA(ctx, 'B'); },

    ASSERT_OWN_SEAT_SELECTED_A: function (ctx) { return assertOwnSeatSelected(ctx, 'A'); },
    ASSERT_OWN_SEAT_SELECTED_B: function (ctx) { return assertOwnSeatSelected(ctx, 'B'); },
    ASSERT_SELECTED_TEXT_HAS_A_SEAT_A: function (ctx) { return assertSelectedTextHasSeat(ctx, 'A', 'A'); },
    ASSERT_SELECTED_TEXT_HAS_B_SEAT_B: function (ctx) { return assertSelectedTextHasSeat(ctx, 'B', 'B'); },
    ASSERT_SEAT_HOLD_TIMER_VISIBLE_A: function (ctx) { return assertTimerVisible(ctx, 'A'); },
    ASSERT_SEAT_HOLD_TIMER_VISIBLE_B: function (ctx) { return assertTimerVisible(ctx, 'B'); },

    ASSERT_A_SEAT_LOCKED_FOR_B: function (ctx) { return assertSeatLockedFor(ctx, 'B', 'A'); },
    ASSERT_B_SEAT_LOCKED_FOR_A: function (ctx) { return assertSeatLockedFor(ctx, 'A', 'B'); },
    TRY_CLICK_A_SEAT_AS_B_EXPECT_BLOCKED: function (ctx) { return tryClickSeatExpectBlocked(ctx, 'B', 'A'); },
    TRY_CLICK_B_SEAT_AS_A_EXPECT_BLOCKED: function (ctx) { return tryClickSeatExpectBlocked(ctx, 'A', 'B'); },

    REFRESH_SEAT_MAP_A: function (ctx) { return refreshSeatMap(ctx, 'A'); },
    REFRESH_SEAT_MAP_B: function (ctx) { return refreshSeatMap(ctx, 'B'); },
    RELEASE_ALL_A: function (ctx) { return releaseAll(ctx, 'A'); },
    RELEASE_ALL_B: function (ctx) { return releaseAll(ctx, 'B'); },
    ASSERT_A_SEAT_AVAILABLE_A: function (ctx) { return assertSeatAvailable(ctx, 'A', 'A'); },
    ASSERT_A_SEAT_AVAILABLE_B: function (ctx) { return assertSeatAvailable(ctx, 'B', 'A'); },
    ASSERT_B_SEAT_AVAILABLE_A: function (ctx) { return assertSeatAvailable(ctx, 'A', 'B'); },
    ASSERT_B_SEAT_AVAILABLE_B: function (ctx) { return assertSeatAvailable(ctx, 'B', 'B'); },

    SUBMIT_BOOKING_NO_SEAT_EXPECT_VALIDATION_A: function (ctx) { return submitBookingExpectNoSeat(ctx, 'A'); },
    SUBMIT_BOOKING_NO_SEAT_EXPECT_VALIDATION_B: function (ctx) { return submitBookingExpectNoSeat(ctx, 'B'); },
    SELECT_PAYMENT_CASH_A: function (ctx) { return selectPayment(ctx, 'A', 'Cash'); },
    SELECT_PAYMENT_CASH_B: function (ctx) { return selectPayment(ctx, 'B', 'Cash'); },
    SELECT_PAYMENT_VNPAY_A: function (ctx) { return selectPayment(ctx, 'A', 'VNPAY'); },
    SELECT_PAYMENT_VNPAY_B: function (ctx) { return selectPayment(ctx, 'B', 'VNPAY'); },
    SELECT_PAYMENT_MOMO_A: function (ctx) { return selectPayment(ctx, 'A', 'MOMO'); },
    SELECT_PAYMENT_MOMO_B: function (ctx) { return selectPayment(ctx, 'B', 'MOMO'); },
    SELECT_PAYMENT_PAYOS_A: function (ctx) { return selectPayment(ctx, 'A', 'PAYOS'); },
    SELECT_PAYMENT_PAYOS_B: function (ctx) { return selectPayment(ctx, 'B', 'PAYOS'); },
    ASSERT_PAYMENT_CASH_A: function (ctx) { return assertPaymentSelected(ctx, 'A', 'Cash'); },
    ASSERT_PAYMENT_VNPAY_A: function (ctx) { return assertPaymentSelected(ctx, 'A', 'VNPAY'); },
    ASSERT_PAYMENT_MOMO_A: function (ctx) { return assertPaymentSelected(ctx, 'A', 'MOMO'); },
    ASSERT_PAYMENT_PAYOS_A: function (ctx) { return assertPaymentSelected(ctx, 'A', 'PAYOS'); },

    WAIT_HOLD_TIMEOUT_A: function (ctx) { return waitHoldTimeout(ctx, 'A'); },
    WAIT_HOLD_TIMEOUT_B: function (ctx) { return waitHoldTimeout(ctx, 'B'); },

    CONNECT_MONGODB: function (ctx) { return connectMongo(ctx); },
    ASSERT_MONGODB_CONNECTED: function (ctx) { return assertMongoConnected(ctx); },
    MONGO_ASSERT_A_SEAT_HOLDING: function (ctx) { return mongoAssertSeatHolding(ctx, 'A'); },
    MONGO_ASSERT_B_SEAT_HOLDING: function (ctx) { return mongoAssertSeatHolding(ctx, 'B'); },
    MONGO_ASSERT_A_SEAT_AVAILABLE: function (ctx) { return mongoAssertSeatAvailable(ctx, 'A'); },
    MONGO_ASSERT_B_SEAT_AVAILABLE: function (ctx) { return mongoAssertSeatAvailable(ctx, 'B'); },
    MONGO_ASSERT_NO_OVERBOOKING_A: function (ctx) { return mongoAssertNoOverbooking(ctx, 'A'); },
    MONGO_ASSERT_NO_OVERBOOKING_B: function (ctx) { return mongoAssertNoOverbooking(ctx, 'B'); },
    MONGO_CLEANUP_HOLDS: function (ctx) { return mongoCleanupHolds(ctx); },
    MONGO_CLEANUP_ALL_QA_HOLDS: function (ctx) { return mongoCleanupAllQaHolds(ctx); },

    ASSERT_EXCEL_RUNTIME: function (ctx) { return assertExcelRuntime(ctx); },
    ASSERT_KEYWORD_MAPPING: function (ctx) { return assertKeywordMapping(ctx); },
    ASSERT_ROUTE_CONFIG: function (ctx) { return assertRouteConfig(ctx); },

    PREPARE_BOOKING_A: function (ctx) { return prepareBooking(ctx, 'A'); },
    PREPARE_BOOKING_B: function (ctx) { return prepareBooking(ctx, 'B'); },
    SETUP_A_HOLDS_SEAT: function (ctx) { return setupAHoldsSeat(ctx); },
    SETUP_A_B_HOLD_DIFFERENT: function (ctx) { return setupABHoldDifferent(ctx); },
    CONCURRENT_SAME_SEAT_A_B_EXPECT_ONE_BLOCKED: function (ctx) { return concurrentSameSeatExpectOneBlocked(ctx); }
};

function loadTestCases() {
    const excelPath = path.join(__dirname, 'SeatLockTestCases.xlsx');
    const workbook = xlsx.readFile(excelPath, { cellDates: true });
    const sheetName = workbook.SheetNames.includes('TestCases') ? 'TestCases' : workbook.SheetNames[0];
    const sheet = workbook.Sheets[sheetName];
    return xlsx.utils.sheet_to_json(sheet, { defval: '' });
}

async function runTestCase(ctx, tc) {
    ctx.current = tc;
    ctx.lastAlert = { A: '', B: '' };
    ctx.holdCodes = {};

    const id = tc.TestCaseID || tc.ID || 'TC';
    const name = tc.TestName || tc.Description || '';
    const flow = splitFlow(tc.ActionFlow);

    if (!flow.length) throw new Error('ActionFlow dang trong.');

    console.log('\n=== ' + id + ': ' + name + ' ===');

    for (let i = 0; i < flow.length; i++) {
        const keyword = flow[i];
        const handler = keywordHandlers[keyword];
        if (!handler) {
            throw new Error('Keyword "' + keyword + '" chua duoc ho tro trong script. Hay dung keyword co trong sheet KeywordLibrary.');
        }
        console.log('[ACTION] ' + keyword);
        await handler(ctx);
    }
}

async function cleanupAfterTest(ctx) {
    await releaseAll(ctx, 'A').catch(function () {});
    await releaseAll(ctx, 'B').catch(function () {});
    await mongoCleanupHolds(ctx).catch(function () {});
}

async function run() {
    console.log('Dang doc file Excel SeatLockTestCases.xlsx...');
    testCases = loadTestCases();
    console.log('Tong testcase trong Excel: ' + testCases.length);

    const ctx = {
        drivers: {},
        vars: {},
        current: {},
        lastAlert: { A: '', B: '' },
        holdCodes: {},
        loggedIn: { A: false, B: false },
        runStamp: new Date().toISOString().replace(/\D/g, '').slice(0, 14),
        db: null,
        mongoClient: null
    };

    try {
        ctx.drivers.A = await createDriver('A');
        ctx.drivers.B = await createDriver('B');
        await connectMongo(ctx);
        await mongoCleanupAllQaHolds(ctx).catch(function () {});

        for (let i = 0; i < testCases.length; i++) {
            const tc = testCases[i];
            const id = tc.TestCaseID || tc.ID || 'TC';
            const name = tc.TestName || tc.Description || '';

            if (!isEnabled(tc.Enabled)) {
                console.log('\n=== ' + id + ': ' + name + ' ===');
                console.log('SKIP: Enabled = NO');
                results.push({ id: id, name: name, status: 'SKIP', message: 'Enabled = NO' });
                continue;
            }

            ctx.vars = {};
            ctx.holdCodes = {};

            try {
                await runTestCase(ctx, tc);
                console.log(id + ' PASS: ' + name);
                results.push({ id: id, name: name, status: 'PASS', message: '' });
            } catch (error) {
                console.log(id + ' FAIL: ' + error.message);
                results.push({ id: id, name: name, status: 'FAIL', message: error.message });
            } finally {
                await cleanupAfterTest(ctx);
            }

            await sleep(delay('delayBetweenTestsMs', 500));
        }
    } finally {
        const passCount = results.filter(function (x) { return x.status === 'PASS'; }).length;
        const failCount = results.filter(function (x) { return x.status === 'FAIL'; }).length;
        const skipCount = results.filter(function (x) { return x.status === 'SKIP'; }).length;

        console.log('\n=== KET QUA AUTOMATION TEST LUONG DAT VE - KHOA GHE ===');
        console.log('Tong so trong Excel: ' + results.length);
        console.log('PASS: ' + passCount);
        console.log('FAIL: ' + failCount);
        console.log('SKIP: ' + skipCount);

        results.filter(function (x) { return x.status === 'FAIL'; }).forEach(function (item) {
            console.log('- ' + item.id + ' FAIL: ' + item.message);
        });

        const reportPath = path.join(__dirname, 'seat-lock-automation-result.json');
        fs.writeFileSync(reportPath, JSON.stringify({ generatedAt: new Date().toISOString(), results: results }, null, 2), 'utf8');
        console.log('Da ghi report: ' + reportPath);

        if (ctx.mongoClient) await ctx.mongoClient.close().catch(function () {});

        if (config.execution && config.execution.keepBrowserOpenAfterRun) {
            console.log('Giu browser mo theo config.keepBrowserOpenAfterRun=true');
        } else {
            if (ctx.drivers.A) await ctx.drivers.A.quit().catch(function () {});
            if (ctx.drivers.B) await ctx.drivers.B.quit().catch(function () {});
        }

        process.exitCode = failCount > 0 ? 1 : 0;
    }
}

run().catch(function (error) {
    console.error('Loi trong qua trinh chay automation:', error);
    process.exitCode = 1;
});
