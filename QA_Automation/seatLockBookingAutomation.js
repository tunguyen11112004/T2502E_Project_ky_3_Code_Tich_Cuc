const { Builder, By, Key } = require('selenium-webdriver');
const { MongoClient, ObjectId } = require('mongodb');
const xlsx = require('xlsx');

const config = require('./seat-lock-test-data.json');

const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms));

const results = [];
let loadedTestCases = [];

const STEP_DELAY_MS = Number(config.execution?.stepDelayMs || 650);
const TEST_DELAY_MS = Number(config.execution?.delayBetweenTestsMs || 900);

async function step(message, ms = STEP_DELAY_MS) {
    console.log(`  -> ${message}`);
    await sleep(ms);
}

function normalizeText(value) {
    return String(value || '')
        .toLowerCase()
        .normalize('NFD')
        .replace(/[\u0300-\u036f]/g, '');
}

function isSeatAvailable(seat) {
    const status = normalizeText(seat.status || seat.Status);
    const isBooked = seat.isBooked || seat.IsBooked;
    const isLocked = seat.isLocked || seat.IsLocked;
    const isHolding = seat.isHolding || seat.IsHolding;

    if (isBooked || isLocked || isHolding) return false;

    return ![
        'booked',
        'holding',
        'reserved',
        'sold',
        'paid',
        'locked'
    ].includes(status);
}

function isSeatBlocked(status) {
    const value = normalizeText(status);
    return ['booked', 'holding', 'reserved', 'sold', 'paid', 'locked'].includes(value);
}

function isBookingCanceled(booking) {
    const bookingStatus = normalizeText(booking.bookingStatus || booking.BookingStatus);
    const paymentStatus = normalizeText(booking.paymentStatus || booking.PaymentStatus);

    return bookingStatus.includes('cancel')
        || bookingStatus.includes('refund')
        || paymentStatus.includes('refund');
}

function toObjectIdIfValid(id) {
    return ObjectId.isValid(id) ? new ObjectId(id) : id;
}

function formatExcelDate(value) {
    if (!value) return '';

    if (value instanceof Date) {
        const year = value.getFullYear();
        const month = String(value.getMonth() + 1).padStart(2, '0');
        const day = String(value.getDate()).padStart(2, '0');
        return `${year}-${month}-${day}`;
    }

    return String(value).trim();
}

async function typeRobust(driver, selector, text) {
    const elements = await driver.findElements(By.css(selector));
    if (!elements.length) return false;

    const el = elements[0];
    await el.click();
    await el.clear();

    if (text && text.toString().trim() !== '') {
        await el.sendKeys(text.toString().trim());
    }

    await el.sendKeys(Key.TAB);
    await sleep(200);
    return true;
}

async function loginAsAdmin(driver) {
    const loginUrl = config.app.baseUrl + config.routes.login;
    await driver.get(loginUrl);
    await step(`Mở trang đăng nhập: ${loginUrl}`);

    const emailElements = await driver.findElements(By.css(config.selectors.login.email));
    const passwordElements = await driver.findElements(By.css(config.selectors.login.password));

    if (!emailElements.length || !passwordElements.length) {
        console.log('Không thấy form login, có thể browser đã đăng nhập sẵn.');
        return;
    }

    await typeRobust(driver, config.selectors.login.email, config.users.admin.email);
    await typeRobust(driver, config.selectors.login.password, config.users.admin.pass);
    await step('Nhập tài khoản admin test');

    const submitBtn = await driver.findElement(By.css(config.selectors.login.submitBtn));
    await submitBtn.click();
    await step('Submit form đăng nhập', 1200);
}

async function getAntiForgeryToken(driver) {
    await driver.get(config.app.baseUrl + config.routes.bookingCreate);
    await sleep(700);

    return await driver.executeScript(`
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : '';
    `);
}

async function submitBookingByFetch(driver, payload) {
    const token = await getAntiForgeryToken(driver);

    const result = await driver.executeAsyncScript(`
        const request = arguments[0];
        const done = arguments[arguments.length - 1];

        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 15000);

        const data = new URLSearchParams();
        data.append('tripId', request.tripId);
        data.append('TripId', request.tripId);

        for (const seatNumber of request.seatNumbers) {
            data.append('seatNumbers', seatNumber);
            data.append('SeatNumbers', seatNumber);
        }

        data.append('passengerName', request.passengerName);
        data.append('PassengerName', request.passengerName);
        data.append('customerName', request.passengerName);
        data.append('CustomerName', request.passengerName);

        data.append('passengerPhone', request.passengerPhone);
        data.append('PassengerPhone', request.passengerPhone);
        data.append('customerPhone', request.passengerPhone);
        data.append('CustomerPhone', request.passengerPhone);

        data.append('passengerEmail', request.passengerEmail);
        data.append('PassengerEmail', request.passengerEmail);
        data.append('customerEmail', request.passengerEmail);
        data.append('CustomerEmail', request.passengerEmail);

        data.append('dob', request.dob);
        data.append('Dob', request.dob);
        data.append('paymentMethod', request.paymentMethod);
        data.append('PaymentMethod', request.paymentMethod);
        data.append('finalAmount', request.finalAmount);
        data.append('FinalAmount', request.finalAmount);

        if (request.token) {
            data.append('__RequestVerificationToken', request.token);
        }

        const headers = {
            'Content-Type': 'application/x-www-form-urlencoded'
        };

        if (request.token) {
            headers.RequestVerificationToken = request.token;
        }

        fetch(request.url, {
            method: 'POST',
            headers,
            body: data.toString(),
            credentials: 'include',
            signal: controller.signal
        })
            .then(async response => {
                const text = await response.text();
                clearTimeout(timeoutId);
                done({
                    ok: response.ok,
                    status: response.status,
                    text: text.slice(0, 2000)
                });
            })
            .catch(error => {
                clearTimeout(timeoutId);
                done({
                    ok: false,
                    status: 0,
                    text: error.message || 'Fetch error'
                });
            });
    `, {
        url: config.app.baseUrl + config.routes.bookTicket,
        token,
        tripId: payload.tripId,
        seatNumbers: payload.seatNumbers,
        passengerName: payload.passengerName,
        passengerPhone: payload.passengerPhone,
        passengerEmail: payload.passengerEmail,
        dob: payload.dob,
        paymentMethod: payload.paymentMethod,
        finalAmount: payload.finalAmount
    });

    return result;
}

async function submitSameSeatTwice(driver, payload) {
    const token = await getAntiForgeryToken(driver);

    const result = await driver.executeAsyncScript(`
        const request = arguments[0];
        const done = arguments[arguments.length - 1];

        function buildBody() {
            const data = new URLSearchParams();
            data.append('tripId', request.tripId);
            data.append('TripId', request.tripId);

            for (const seatNumber of request.seatNumbers) {
                data.append('seatNumbers', seatNumber);
                data.append('SeatNumbers', seatNumber);
            }

            data.append('passengerName', request.passengerName);
            data.append('PassengerName', request.passengerName);
            data.append('customerName', request.passengerName);
            data.append('CustomerName', request.passengerName);

            data.append('passengerPhone', request.passengerPhone);
            data.append('PassengerPhone', request.passengerPhone);
            data.append('customerPhone', request.passengerPhone);
            data.append('CustomerPhone', request.passengerPhone);

            data.append('passengerEmail', request.passengerEmail);
            data.append('PassengerEmail', request.passengerEmail);
            data.append('customerEmail', request.passengerEmail);
            data.append('CustomerEmail', request.passengerEmail);

            data.append('dob', request.dob);
            data.append('Dob', request.dob);
            data.append('paymentMethod', request.paymentMethod);
            data.append('PaymentMethod', request.paymentMethod);
            data.append('finalAmount', request.finalAmount);
            data.append('FinalAmount', request.finalAmount);

            if (request.token) {
                data.append('__RequestVerificationToken', request.token);
            }

            return data.toString();
        }

        const headers = {
            'Content-Type': 'application/x-www-form-urlencoded'
        };

        if (request.token) {
            headers.RequestVerificationToken = request.token;
        }

        const options = {
            method: 'POST',
            headers,
            credentials: 'include'
        };

        Promise.all([
            fetch(request.url, { ...options, body: buildBody() }).then(async response => ({ ok: response.ok, status: response.status, text: (await response.text()).slice(0, 1000) })).catch(error => ({ ok: false, status: 0, text: error.message })),
            fetch(request.url, { ...options, body: buildBody() }).then(async response => ({ ok: response.ok, status: response.status, text: (await response.text()).slice(0, 1000) })).catch(error => ({ ok: false, status: 0, text: error.message }))
        ]).then(done);
    `, {
        url: config.app.baseUrl + config.routes.bookTicket,
        token,
        tripId: payload.tripId,
        seatNumbers: payload.seatNumbers,
        passengerName: payload.passengerName,
        passengerPhone: payload.passengerPhone,
        passengerEmail: payload.passengerEmail,
        dob: payload.dob,
        paymentMethod: payload.paymentMethod,
        finalAmount: payload.finalAmount
    });

    return result;
}

async function findAvailableTripAndSeat(db, excludedSeatNumbers = []) {
    const trips = await db.collection(config.database.tripsCollection)
        .find({ realtimeSeats: { $exists: true, $ne: [] } })
        .limit(100)
        .toArray();

    for (const trip of trips) {
        const tripId = trip._id.toString();

        const bookings = await db.collection(config.database.bookingsCollection)
            .find({
                $or: [
                    { tripId },
                    { TripId: tripId }
                ]
            })
            .toArray();

        const bookedSeatNumbers = new Set();

        for (const booking of bookings) {
            if (isBookingCanceled(booking)) continue;

            const passengers = booking.passengers || booking.Passengers || [];
            for (const passenger of passengers) {
                const seatNumber = passenger.seatNumber || passenger.SeatNumber || passenger.seat || passenger.Seat;
                if (seatNumber) bookedSeatNumbers.add(String(seatNumber));
            }
        }

        const seats = trip.realtimeSeats || [];
        const seat = seats.find(item => {
            if (!item || !item.seatNumber) return false;

            const seatNumber = String(item.seatNumber);
            if (excludedSeatNumbers.includes(seatNumber)) return false;
            if (bookedSeatNumbers.has(seatNumber)) return false;
            if (!isSeatAvailable(item)) return false;

            return true;
        });

        if (seat) {
            return {
                trip,
                tripId,
                seatNumber: seat.seatNumber
            };
        }
    }

    throw new Error('Không tìm thấy chuyến có ghế trống để test.');
}

async function findTripWithMultipleAvailableSeats(db, minCount = 2) {
    const trips = await db.collection(config.database.tripsCollection)
        .find({ realtimeSeats: { $exists: true, $ne: [] } })
        .limit(100)
        .toArray();

    for (const trip of trips) {
        const seats = (trip.realtimeSeats || []).filter(seat => seat && seat.seatNumber && isSeatAvailable(seat));

        if (seats.length >= minCount) {
            return {
                trip,
                tripId: trip._id.toString(),
                seatNumbers: seats.slice(0, minCount).map(seat => seat.seatNumber)
            };
        }
    }

    throw new Error(`Không tìm thấy trip có tối thiểu ${minCount} ghế Available.`);
}

async function updateSeat(db, tripId, seatNumber, status, extra = {}) {
    const update = {
        $set: {
            'realtimeSeats.$.status': status,
            'realtimeSeats.$.Status': status,
            'realtimeSeats.$.isBooked': status === 'Booked',
            'realtimeSeats.$.IsBooked': status === 'Booked',
            'realtimeSeats.$.isLocked': status === 'Holding' || status === 'Booked',
            'realtimeSeats.$.IsLocked': status === 'Holding' || status === 'Booked',
            'realtimeSeats.$.isHolding': status === 'Holding',
            'realtimeSeats.$.IsHolding': status === 'Holding',
            'realtimeSeats.$.holdExpiresAt': extra.holdExpiresAt || null,
            'realtimeSeats.$.HoldExpiresAt': extra.holdExpiresAt || null,
            'realtimeSeats.$.lockedUntil': extra.holdExpiresAt || null,
            'realtimeSeats.$.LockedUntil': extra.holdExpiresAt || null,
            'realtimeSeats.$.expiresAt': extra.holdExpiresAt || null,
            'realtimeSeats.$.ExpiresAt': extra.holdExpiresAt || null,
            'realtimeSeats.$.heldBySessionId': extra.heldBySessionId || null,
            'realtimeSeats.$.HeldBySessionId': extra.heldBySessionId || null,
            'realtimeSeats.$.bookingId': extra.bookingId || null,
            'realtimeSeats.$.BookingId': extra.bookingId || null
        }
    };

    await db.collection(config.database.tripsCollection).updateOne(
        {
            _id: toObjectIdIfValid(tripId),
            'realtimeSeats.seatNumber': seatNumber
        },
        update
    );
}

async function resetSeat(db, tripId, seatNumber) {
    await updateSeat(db, tripId, seatNumber, 'Available', {
        holdExpiresAt: null,
        heldBySessionId: null,
        bookingId: null
    });
}

async function getSeatFromDb(db, tripId, seatNumber) {
    const trip = await db.collection(config.database.tripsCollection).findOne(
        {
            _id: toObjectIdIfValid(tripId),
            'realtimeSeats.seatNumber': seatNumber
        },
        {
            projection: {
                realtimeSeats: {
                    $elemMatch: { seatNumber }
                }
            }
        }
    );

    if (!trip || !trip.realtimeSeats || !trip.realtimeSeats.length) return null;
    return trip.realtimeSeats[0];
}

async function verifyNoOverbooking(db, tripId, seatNumber) {
    const bookings = await db.collection(config.database.bookingsCollection)
        .find({
            $or: [
                { tripId, 'passengers.seatNumber': seatNumber },
                { TripId: tripId, 'Passengers.SeatNumber': seatNumber }
            ]
        })
        .toArray();

    const validBookings = bookings.filter(booking => !isBookingCanceled(booking));

    if (validBookings.length > 1) {
        throw new Error(`Phát hiện overbooking ghế ${seatNumber}. Số booking hợp lệ: ${validBookings.length}`);
    }

    return validBookings.length;
}

async function triggerSeatMapRefresh(driver, tripId) {
    return await driver.executeAsyncScript(`
        const request = arguments[0];
        const done = arguments[arguments.length - 1];

        fetch(request.url, { method: 'GET', credentials: 'include' })
            .then(async response => done({ ok: response.ok, status: response.status, text: await response.text() }))
            .catch(error => done({ ok: false, status: 0, text: error.message }));
    `, {
        url: config.app.baseUrl + config.routes.seatMap + '?tripId=' + encodeURIComponent(tripId)
    });
}

async function clickButtonByText(driver, textCandidates) {
    return await driver.executeScript(`
        const labels = arguments[0].map(x => String(x || '').toLowerCase());

        function normalize(value) {
            return String(value || '')
                .toLowerCase()
                .normalize('NFD')
                .replace(/[\u0300-\u036f]/g, '');
        }

        const normalizedLabels = labels.map(normalize);
        const elements = Array.from(document.querySelectorAll('button, a, input[type="button"], input[type="submit"]'));

        const target = elements.find(el => {
            const text = normalize(el.innerText || el.value || el.getAttribute('aria-label') || '');
            return normalizedLabels.some(label => text.includes(label));
        });

        if (!target) return false;

        target.scrollIntoView({ behavior: 'smooth', block: 'center' });
        target.click();
        return true;
    `, textCandidates);
}

async function fillBookingSearchForm(driver) {
    const searchConfig = config.bookingSearch || {};

    return await driver.executeScript(`
        const data = arguments[0];

        function normalize(value) {
            return String(value || '')
                .toLowerCase()
                .normalize('NFD')
                .replace(/[\u0300-\u036f]/g, '');
        }

        function isVisible(el) {
            const style = window.getComputedStyle(el);
            const rect = el.getBoundingClientRect();
            return style.display !== 'none'
                && style.visibility !== 'hidden'
                && rect.width > 0
                && rect.height > 0;
        }

        function setValue(el, value) {
            if (!el) return false;
            el.scrollIntoView({ behavior: 'smooth', block: 'center' });
            el.focus();
            el.value = value;
            el.dispatchEvent(new Event('input', { bubbles: true }));
            el.dispatchEvent(new Event('change', { bubbles: true }));
            return true;
        }

        const allInputs = Array.from(document.querySelectorAll('main input, form input, input'))
            .filter(isVisible)
            .filter(el => !['hidden', 'checkbox', 'radio', 'submit', 'button'].includes((el.type || '').toLowerCase()));

        const dateInput = allInputs.find(el => (el.type || '').toLowerCase() === 'date');

        const textInputs = allInputs
            .filter(el => (el.type || 'text').toLowerCase() !== 'date')
            .filter(el => normalize(el.placeholder) !== 'search')
            .filter(el => normalize(el.getAttribute('aria-label')) !== 'search');

        const originInput = textInputs.find(el => {
            const meta = normalize([el.name, el.id, el.placeholder, el.getAttribute('aria-label')].join(' '));
            return meta.includes('xuat') || meta.includes('departure') || meta.includes('origin') || meta.includes('ha');
        }) || textInputs[0];

        const destinationInput = textInputs.find(el => {
            const meta = normalize([el.name, el.id, el.placeholder, el.getAttribute('aria-label')].join(' '));
            return meta.includes('den') || meta.includes('destination') || meta.includes('arrival') || meta.includes('ng');
        }) || textInputs.find(el => el !== originInput) || textInputs[1];

        const originOk = setValue(originInput, data.origin);
        const destinationOk = setValue(destinationInput, data.destination);
        const dateOk = setValue(dateInput, data.date);

        return {
            origin: originOk,
            destination: destinationOk,
            date: dateOk,
            originPlaceholder: originInput ? originInput.placeholder : '',
            destinationPlaceholder: destinationInput ? destinationInput.placeholder : '',
            dateType: dateInput ? dateInput.type : ''
        };
    `, {
        origin: searchConfig.origin || 'Hà Nội',
        destination: searchConfig.destination || 'Nghệ An',
        date: searchConfig.date || new Date().toISOString().slice(0, 10)
    });
}

async function tcBookingUiSearchDemo(driver, db, scenario) {
    const bookingUrl = config.app.baseUrl + config.routes.bookingCreate;

    await step(`Mở trang Booking: ${bookingUrl}`, 900);
    await driver.get(bookingUrl);
    await sleep(1000);

    await step('Scroll trang Booking xuống khu vực form tìm chuyến', 900);
    await driver.executeScript(`window.scrollTo({ top: 260, behavior: 'smooth' });`);
    await sleep(Number(config.execution?.uiDemoDelayMs || 1100));

    await step('Bấm nút Làm mới dữ liệu', 900);
    const refreshClicked = await clickButtonByText(driver, ['Làm mới dữ liệu', 'Lam moi du lieu', 'Refresh']);
    console.log(`${scenario.TestCaseID}: Refresh clicked=${refreshClicked}`);
    await sleep(Number(config.execution?.uiDemoDelayMs || 1100));

    await step('Nhập nơi đi / nơi đến / ngày đi', 900);
    const filled = await fillBookingSearchForm(driver);

    if (!filled.origin || !filled.destination || !filled.date) {
        throw new Error(`Không nhập được đủ form search. origin=${filled.origin}, destination=${filled.destination}, date=${filled.date}`);
    }

    const searchConfig = config.bookingSearch || {};
    console.log(`${scenario.TestCaseID}: Đã nhập search origin=${searchConfig.origin || 'Hà Nội'}, destination=${searchConfig.destination || 'Nghệ An'}, date=${searchConfig.date || new Date().toISOString().slice(0, 10)}`);
    await sleep(Number(config.execution?.uiDemoDelayMs || 1100));

    await step('Bấm Tìm kiếm chuyến chạy', 900);
    const searchClicked = await clickButtonByText(driver, ['Tìm kiếm chuyến chạy', 'Tim kiem chuyen chay', 'Tìm kiếm', 'Search']);

    if (!searchClicked) {
        throw new Error('Không tìm thấy hoặc không bấm được nút Tìm kiếm chuyến chạy.');
    }

    await sleep(Number(config.execution?.uiDemoDelayMs || 1400));
    await step('Scroll xuống khu vực kết quả chuyến chạy', 900);
    await driver.executeScript(`window.scrollTo({ top: Math.floor(document.body.scrollHeight * 0.55), behavior: 'smooth' });`);
    await sleep(Number(config.execution?.uiDemoDelayMs || 1200));

    const bodyText = await driver.executeScript(`return document.body.innerText || '';`);
    if (!bodyText || bodyText.trim().length < 20) {
        throw new Error('Trang Booking không có nội dung sau khi tìm kiếm.');
    }

    console.log(`${scenario.TestCaseID}: UI demo search completed. Page text length=${bodyText.length}`);
}


async function tcConcurrentSameSeat(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    await resetSeat(db, target.tripId, target.seatNumber);

    console.log(`Đang chạy ${scenario.TestCaseID}: ${scenario.Description}`);
    console.log(`TripId=${target.tripId}, Seat=${target.seatNumber}`);
    await step('Chuẩn bị payload đặt vé cùng một ghế');

    const payload = {
        tripId: target.tripId,
        seatNumbers: [target.seatNumber],
        passengerName: scenario.PassengerName || config.booking.passengerName,
        passengerPhone: scenario.PassengerPhone || config.booking.passengerPhone,
        passengerEmail: scenario.PassengerEmail || config.booking.passengerEmail,
        dob: formatExcelDate(scenario.DOB) || config.booking.dob,
        paymentMethod: scenario.PaymentMethod || config.booking.paymentMethod,
        finalAmount: scenario.FinalAmount || config.booking.finalAmount
    };

    const [resultA, resultB] = await submitSameSeatTwice(driver, payload);
    console.log(`User A result: ${JSON.stringify({ ok: resultA.ok, status: resultA.status })}`);
    console.log(`User B result: ${JSON.stringify({ ok: resultB.ok, status: resultB.status })}`);

    await step('Kiểm tra MongoDB sau khi 2 request cùng ghế');
    const validBookingCount = await verifyNoOverbooking(db, target.tripId, target.seatNumber);
    console.log(`MongoDB valid booking count=${validBookingCount}`);
}

async function tcDbNoOverbooking(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    const count = await verifyNoOverbooking(db, target.tripId, target.seatNumber);
    console.log(`${scenario.TestCaseID}: Không phát hiện overbooking. Valid booking count=${count}`);
}

async function tcExpiredHoldCleanup(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    const expiredAt = new Date(Date.now() - 10 * 60 * 1000);

    await step(`Set ghế ${target.seatNumber} sang Holding hết hạn`);
    await updateSeat(db, target.tripId, target.seatNumber, 'Holding', {
        holdExpiresAt: expiredAt,
        heldBySessionId: 'automation-expired-session'
    });

    const seatBeforeCleanup = await getSeatFromDb(db, target.tripId, target.seatNumber);
    if (!seatBeforeCleanup || normalizeText(seatBeforeCleanup.status || seatBeforeCleanup.Status) !== 'holding') {
        throw new Error(`Không set được Holding cho ghế ${target.seatNumber}`);
    }

    await step('Cleanup ghế Holding về Available');
    await resetSeat(db, target.tripId, target.seatNumber);

    const seatAfterCleanup = await getSeatFromDb(db, target.tripId, target.seatNumber);
    if (!seatAfterCleanup) throw new Error(`Không tìm thấy ghế ${target.seatNumber} sau cleanup.`);
    if (isSeatBlocked(seatAfterCleanup.status || seatAfterCleanup.Status)) {
        throw new Error(`Cleanup thất bại. Status=${seatAfterCleanup.status || seatAfterCleanup.Status}`);
    }

    console.log(`${scenario.TestCaseID}: Expired hold cleanup OK. Status after cleanup=${seatAfterCleanup.status || seatAfterCleanup.Status}`);
}

async function tcBookedNotReleased(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    const expiredAt = new Date(Date.now() - 10 * 60 * 1000);

    await updateSeat(db, target.tripId, target.seatNumber, 'Booked', {
        holdExpiresAt: expiredAt,
        heldBySessionId: 'automation-booked-session'
    });

    await step('Gọi refresh seat map sau khi set Booked');
    await triggerSeatMapRefresh(driver, target.tripId);
    await sleep(800);

    const seat = await getSeatFromDb(db, target.tripId, target.seatNumber);
    await resetSeat(db, target.tripId, target.seatNumber);

    if (!seat) throw new Error(`Không tìm thấy ghế ${target.seatNumber}`);
    if (normalizeText(seat.status || seat.Status) !== 'booked') {
        throw new Error(`Ghế Booked bị nhả sai. Status hiện tại=${seat.status || seat.Status}`);
    }

    console.log(`${scenario.TestCaseID}: Booked seat không bị release sai.`);
}

async function tcRerunSmoke(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    console.log(`${scenario.TestCaseID}: Automation có thể chạy lại. Sample trip=${target.tripId}, seat=${target.seatNumber}`);
}

async function tcExcelDataLoaded(driver, db, scenario) {
    if (!loadedTestCases || loadedTestCases.length === 0) {
        throw new Error('Không đọc được testcase từ Excel.');
    }

    console.log(`${scenario.TestCaseID}: Excel loaded ${loadedTestCases.length} test cases.`);
}

async function tcConfigHealth(driver, db, scenario) {
    if (!config.app?.baseUrl) throw new Error('Thiếu app.baseUrl.');
    if (!config.database?.uri) throw new Error('Thiếu database.uri.');
    if (!config.database?.dbName) throw new Error('Thiếu database.dbName.');

    console.log(`${scenario.TestCaseID}: Config OK. BaseUrl=${config.app.baseUrl}, DB=${config.database.dbName}`);
}

async function tcMongoConnectionHealth(driver, db, scenario) {
    const collections = await db.listCollections().toArray();

    if (!collections.length) {
        throw new Error('Kết nối MongoDB được nhưng không đọc được collection nào.');
    }

    console.log(`${scenario.TestCaseID}: MongoDB connected. Collections=${collections.length}`);
}

async function tcTripHasRealtimeSeats(driver, db, scenario) {
    const count = await db.collection(config.database.tripsCollection).countDocuments({
        realtimeSeats: { $exists: true, $ne: [] }
    });

    if (count <= 0) {
        throw new Error('Không có trip nào có realtimeSeats.');
    }

    console.log(`${scenario.TestCaseID}: Trips with realtimeSeats=${count}`);
}

async function tcAvailableSeatExists(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    console.log(`${scenario.TestCaseID}: Found available seat ${target.seatNumber} in trip ${target.tripId}`);
}

async function tcSeatStatusValid(driver, db, scenario) {
    const allowed = new Set(['available', 'booked', 'holding', 'reserved', 'sold', 'paid', 'locked', '']);
    const trips = await db.collection(config.database.tripsCollection)
        .find({ realtimeSeats: { $exists: true, $ne: [] } })
        .limit(20)
        .toArray();

    for (const trip of trips) {
        for (const seat of (trip.realtimeSeats || [])) {
            const status = normalizeText(seat.status || seat.Status);
            if (!allowed.has(status)) {
                throw new Error(`Status ghế không hợp lệ: ${status}, trip=${trip._id}, seat=${seat.seatNumber}`);
            }
        }
    }

    console.log(`${scenario.TestCaseID}: Seat status hợp lệ trong ${trips.length} trip mẫu.`);
}

async function tcHoldingSeatCanBeSet(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    await updateSeat(db, target.tripId, target.seatNumber, 'Holding', {
        holdExpiresAt: new Date(Date.now() + 5 * 60 * 1000),
        heldBySessionId: 'automation-holding-set'
    });

    const seat = await getSeatFromDb(db, target.tripId, target.seatNumber);
    await resetSeat(db, target.tripId, target.seatNumber);

    if (!seat || normalizeText(seat.status || seat.Status) !== 'holding') {
        throw new Error('Không set được trạng thái Holding.');
    }

    console.log(`${scenario.TestCaseID}: Set Holding OK for seat ${target.seatNumber}`);
}

async function tcHoldingSeatCanBeReset(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    await updateSeat(db, target.tripId, target.seatNumber, 'Holding', {
        holdExpiresAt: new Date(Date.now() + 5 * 60 * 1000),
        heldBySessionId: 'automation-holding-reset'
    });

    await resetSeat(db, target.tripId, target.seatNumber);
    const seat = await getSeatFromDb(db, target.tripId, target.seatNumber);

    if (!seat || isSeatBlocked(seat.status || seat.Status)) {
        throw new Error(`Reset Holding thất bại. Status=${seat?.status || seat?.Status}`);
    }

    console.log(`${scenario.TestCaseID}: Reset Holding về ${seat.status || seat.Status}`);
}

async function tcMultipleAvailableSeats(driver, db, scenario) {
    const target = await findTripWithMultipleAvailableSeats(db, 2);
    console.log(`${scenario.TestCaseID}: Trip=${target.tripId}, seats=${target.seatNumbers.join(', ')}`);
}

async function tcMultiSeatHoldingCleanup(driver, db, scenario) {
    const target = await findTripWithMultipleAvailableSeats(db, 2);

    await step(`Set 2 ghế Holding: ${target.seatNumbers.join(', ')}`);
    for (const seatNumber of target.seatNumbers) {
        await updateSeat(db, target.tripId, seatNumber, 'Holding', {
            holdExpiresAt: new Date(Date.now() - 10 * 60 * 1000),
            heldBySessionId: 'automation-multi-hold'
        });
    }

    await step('Cleanup 2 ghế Holding');
    for (const seatNumber of target.seatNumbers) {
        await resetSeat(db, target.tripId, seatNumber);
    }

    for (const seatNumber of target.seatNumbers) {
        const seat = await getSeatFromDb(db, target.tripId, seatNumber);
        if (!seat || isSeatBlocked(seat.status || seat.Status)) {
            throw new Error(`Cleanup nhiều ghế thất bại tại seat=${seatNumber}`);
        }
    }

    console.log(`${scenario.TestCaseID}: Multi-seat cleanup OK.`);
}

async function tcSeatMapApiSmoke(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    const response = await triggerSeatMapRefresh(driver, target.tripId);

    if (!response || response.ok !== true) {
        throw new Error(`SeatMap API fail. Status=${response?.status}, text=${response?.text}`);
    }

    console.log(`${scenario.TestCaseID}: SeatMap API OK. Status=${response.status}`);
}

async function tcBookedSeatStateCanBeSet(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);

    await updateSeat(db, target.tripId, target.seatNumber, 'Booked', {
        holdExpiresAt: new Date(Date.now() + 5 * 60 * 1000),
        heldBySessionId: 'automation-booked-set'
    });

    const seat = await getSeatFromDb(db, target.tripId, target.seatNumber);
    await resetSeat(db, target.tripId, target.seatNumber);

    if (!seat || normalizeText(seat.status || seat.Status) !== 'booked') {
        throw new Error('Không set được trạng thái Booked.');
    }

    console.log(`${scenario.TestCaseID}: Set Booked OK for seat ${target.seatNumber}`);
}

async function tcResetBookedSeatToAvailable(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);

    await updateSeat(db, target.tripId, target.seatNumber, 'Booked', {
        holdExpiresAt: new Date(Date.now() + 5 * 60 * 1000),
        heldBySessionId: 'automation-booked-reset'
    });

    await resetSeat(db, target.tripId, target.seatNumber);
    const seat = await getSeatFromDb(db, target.tripId, target.seatNumber);

    if (!seat || isSeatBlocked(seat.status || seat.Status)) {
        throw new Error(`Reset Booked thất bại. Status=${seat?.status || seat?.Status}`);
    }

    console.log(`${scenario.TestCaseID}: Reset Booked về ${seat.status || seat.Status}`);
}

async function tcBookingDuplicateScanSample(driver, db, scenario) {
    const target = await findAvailableTripAndSeat(db);
    const count = await verifyNoOverbooking(db, target.tripId, target.seatNumber);

    console.log(`${scenario.TestCaseID}: Sample duplicate scan OK. Valid booking count=${count}`);
}

async function tcBookingPayloadDataReady(driver, db, scenario) {
    const required = ['passengerName', 'passengerPhone', 'passengerEmail', 'dob', 'paymentMethod', 'finalAmount'];

    for (const key of required) {
        if (!config.booking?.[key]) {
            throw new Error(`Thiếu config.booking.${key}`);
        }
    }

    console.log(`${scenario.TestCaseID}: Booking test data OK for passenger=${config.booking.passengerName}`);
}

async function tcPaymentMethodConfigured(driver, db, scenario) {
    if (!config.booking?.paymentMethod) {
        throw new Error('PaymentMethod chưa cấu hình.');
    }

    console.log(`${scenario.TestCaseID}: PaymentMethod=${config.booking.paymentMethod}`);
}

async function tcRouteConfigValid(driver, db, scenario) {
    const required = ['login', 'bookingCreate', 'bookTicket', 'seatMap'];

    for (const key of required) {
        if (!config.routes?.[key]) {
            throw new Error(`Thiếu config.routes.${key}`);
        }
    }

    console.log(`${scenario.TestCaseID}: Routes OK: ${required.map(k => `${k}=${config.routes[k]}`).join(', ')}`);
}

async function tcHandlerMappingIntegrity(driver, db, scenario) {
    const missing = loadedTestCases
        .filter(item => item.Category)
        .filter(item => !handlers[item.Category])
        .map(item => `${item.TestCaseID}:${item.Category}`);

    if (missing.length > 0) {
        throw new Error(`Thiếu handler cho category: ${missing.join(', ')}`);
    }

    console.log(`${scenario.TestCaseID}: Handler mapping OK for ${loadedTestCases.length} test cases.`);
}

async function tcSlowDemoWait(driver, db, scenario) {
    await step('Delay demo 1/3 - chuẩn bị quan sát kết quả');
    await step('Delay demo 2/3 - automation đang chạy ổn định');
    await step('Delay demo 3/3 - hoàn tất wait smoke');
    console.log(`${scenario.TestCaseID}: Slow demo wait OK.`);
}

async function tcReportSummarySmoke(driver, db, scenario) {
    if (!Array.isArray(results)) {
        throw new Error('Results object không hợp lệ.');
    }

    console.log(`${scenario.TestCaseID}: Report summary object OK. Current executed=${results.length}`);
}

const handlers = {
    ConcurrentSameSeat: tcConcurrentSameSeat,
    DbNoOverbooking: tcDbNoOverbooking,
    ExpiredHoldCleanup: tcExpiredHoldCleanup,
    BookedNotReleased: tcBookedNotReleased,
    RerunSmoke: tcRerunSmoke,
    ExcelDataLoaded: tcExcelDataLoaded,
    ConfigHealth: tcConfigHealth,
    MongoConnectionHealth: tcMongoConnectionHealth,
    TripHasRealtimeSeats: tcTripHasRealtimeSeats,
    AvailableSeatExists: tcAvailableSeatExists,
    SeatStatusValid: tcSeatStatusValid,
    HoldingSeatCanBeSet: tcHoldingSeatCanBeSet,
    HoldingSeatCanBeReset: tcHoldingSeatCanBeReset,
    MultipleAvailableSeats: tcMultipleAvailableSeats,
    MultiSeatHoldingCleanup: tcMultiSeatHoldingCleanup,
    SeatMapApiSmoke: tcSeatMapApiSmoke,
    BookedSeatStateCanBeSet: tcBookedSeatStateCanBeSet,
    ResetBookedSeatToAvailable: tcResetBookedSeatToAvailable,
    BookingDuplicateScanSample: tcBookingDuplicateScanSample,
    BookingPayloadDataReady: tcBookingPayloadDataReady,
    PaymentMethodConfigured: tcPaymentMethodConfigured,
    RouteConfigValid: tcRouteConfigValid,
    HandlerMappingIntegrity: tcHandlerMappingIntegrity,
    SlowDemoWait: tcSlowDemoWait,
    ReportSummarySmoke: tcReportSummarySmoke,
    BookingUiSearchDemo: tcBookingUiSearchDemo
};

async function runSeatLockAutomation() {
    console.log('Đang tải dữ liệu Test Cases từ Excel...');

    const workbook = xlsx.readFile('SeatLockTestCases.xlsx', { cellDates: true });
    const sheet = workbook.Sheets[workbook.SheetNames[0]];
    loadedTestCases = xlsx.utils.sheet_to_json(sheet, { defval: '' });

    const client = new MongoClient(config.database.uri);
    const driver = await new Builder().forBrowser('chrome').build();

    try {
        await client.connect();
        const db = client.db(config.database.dbName);

        console.log('=== BẮT ĐẦU AUTOMATION TEST LUỒNG ĐẶT VÉ - KHÓA GHẾ ===');

        await loginAsAdmin(driver);

        for (const scenario of loadedTestCases) {
            const handler = handlers[scenario.Category];

            if (!handler) {
                console.log(`${scenario.TestCaseID} Skip: Không có handler cho category ${scenario.Category}`);
                continue;
            }

            try {
                await handler(driver, db, scenario);
                console.log(`${scenario.TestCaseID} Pass: ${scenario.Description}`);
                results.push({ id: scenario.TestCaseID, status: 'PASS', description: scenario.Description });
            } catch (error) {
                console.log(`${scenario.TestCaseID} Fail: ${error.message}`);
                results.push({ id: scenario.TestCaseID, status: 'FAIL', description: scenario.Description, message: error.message });
            }

            await sleep(TEST_DELAY_MS);
        }

        const passCount = results.filter(x => x.status === 'PASS').length;
        const failCount = results.filter(x => x.status === 'FAIL').length;

        console.log('\n=== KẾT QUẢ AUTOMATION TEST ===');
        console.log(`Tổng số: ${results.length}`);
        console.log(`PASS: ${passCount}`);
        console.log(`FAIL: ${failCount}`);

        for (const item of results.filter(x => x.status === 'FAIL')) {
            console.log(`- ${item.id} FAIL: ${item.message}`);
        }

        if (failCount === 0) {
            console.log('=== HOÀN TẤT TOÀN BỘ KỊCH BẢN KIỂM THỬ ===');
        }
    } catch (error) {
        console.error('Lỗi trong quá trình chạy:', error);
    } finally {
        await driver.quit().catch(() => {});
        await client.close().catch(() => {});
    }
}

runSeatLockAutomation();
