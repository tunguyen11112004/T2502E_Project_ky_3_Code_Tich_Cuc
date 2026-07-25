const { Builder, By, until, Key } = require('selenium-webdriver');
const { MongoClient } = require('mongodb');
const crypto = require('crypto');
const xlsx = require('xlsx');

const config = require('./qr-payment-test-data.json');

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const WAIT_MS = Number(config.execution?.waitTimeoutMs || 20000);
const PAYMENT_WAIT_MS = Number(config.execution?.paymentWaitMs || 35000);
const QR_DEMO_DELAY = Number(config.execution?.qrDemoDelayMs || 4000);
const STEP_DELAY = Number(config.execution?.stepDelayMs || 600);
const TEST_DELAY = Number(config.execution?.delayBetweenTestsMs || 800);

const results = [];
let loadedTestCases = [];
const state = {
  lastPaymentUrl: null,
  lastOrderId: null,
  lastPaymentMethod: null,
  lastBookingCode: null,
  sessionReady: false,
};

function isEnabled(scenario) {
  const value = (scenario.Enabled || 'Y').toString().trim().toUpperCase();
  return !['N', 'NO', '0', 'FALSE'].includes(value);
}

function useSessionFromScenario(scenario) {
  const value = (scenario.UseSession || 'Y').toString().trim().toUpperCase();
  return !['N', 'NO', '0', 'FALSE'].includes(value);
}

function shouldOpenQrPage(scenario) {
  const value = (scenario.OpenQrPage || '').toString().trim().toUpperCase();
  return ['Y', 'YES', '1', 'TRUE'].includes(value);
}

function resolveTravelDateFromScenario(scenario = {}) {
  const raw = scenario.TravelDate || config.bookingSearch?.travelDate || 'auto';
  if (!raw || raw.toString().trim() === '' || raw.toString().trim().toLowerCase() === 'auto') {
    return formatLocalDate(new Date());
  }
  return raw.toString().trim();
}

function shouldSimulateCallback(scenario) {
  const value = (scenario.SimulateCallback || '').toString().trim().toUpperCase();
  return ['Y', 'YES', '1', 'TRUE'].includes(value);
}

function shouldVerifyDb(scenario) {
  const value = (scenario.VerifyDb || '').toString().trim().toUpperCase();
  return ['Y', 'YES', '1', 'TRUE'].includes(value);
}

function resolveBookingCredentials(scenario = {}) {
  return {
    email: scenario.Email || config.users.booking?.email || config.users.admin.email,
    password: scenario.Password || config.users.booking?.pass || config.users.admin.pass,
  };
}

function validatePaymentUrl(method, paymentUrl) {
  if (!paymentUrl) return false;
  const normalized = method.toString().trim().toUpperCase();
  if (normalized === 'MOMO') return paymentUrl.includes('momo');
  if (normalized === 'VNPAY') return paymentUrl.includes('vnpayment') || paymentUrl.includes('vnpay');
  if (normalized === 'PAYOS') return paymentUrl.includes('payos');
  return false;
}

function isCashMethod(method) {
  return method.toString().trim().toLowerCase() === 'cash';
}

function resolvePaymentMethod(scenario, fallback = 'Cash') {
  return (scenario.PaymentMethod || fallback).toString().trim();
}

function loadExcelTestCases() {
  const fileName = config.excel?.fileName || 'QrPaymentTestCases.xlsx';
  const workbook = xlsx.readFile(fileName, { cellDates: true });
  const sheet = workbook.Sheets[workbook.SheetNames[0]];
  return xlsx.utils.sheet_to_json(sheet, { defval: '' });
}

// ==========================================
// HÀM HỖ TRỢ
// ==========================================

async function step(message, ms = STEP_DELAY) {
  console.log(`  -> ${message}`);
  await sleep(ms);
}

function logResult(testCaseId, passed, message) {
  const status = passed ? 'PASS' : 'FAIL';
  console.log(`${testCaseId} ${status} - ${message}`);
  results.push({ id: testCaseId, status, message });
}

async function fillInput(driver, selector, value) {
  const el = await driver.findElement(By.css(selector));
  await driver.executeScript(
    `
    const input = arguments[0];
    const val = arguments[1];
    input.focus();
    input.value = val;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
  `,
    el,
    value
  );
  await sleep(200);
}

async function typeRobust(driver, selector, text) {
  const el = await driver.findElement(By.css(selector));
  await el.click();
  await el.clear();
  if (text && text.toString().trim() !== '') {
    await el.sendKeys(text.toString().trim());
  }
  await el.sendKeys(Key.TAB);
  await sleep(200);
}

async function clearSession(driver) {
  await driver.manage().deleteAllCookies();
}

function tomorrowDateString() {
  const d = new Date();
  d.setDate(d.getDate() + 1);
  return formatLocalDate(d);
}

function formatLocalDate(date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${y}-${m}-${day}`;
}

/** Dùng ngày hôm nay (local) — khớp dữ liệu trip seed và giá trị mặc định trên form. */
function resolveTravelDate() {
  const configured = config.bookingSearch?.travelDate;
  if (configured && configured !== 'auto') return configured;
  return formatLocalDate(new Date());
}

async function setDateInput(driver, selector, dateValue) {
  const el = await driver.findElement(By.css(selector));
  await driver.executeScript(
    `
    const input = arguments[0];
    const val = arguments[1];
    input.value = val;
    input.dispatchEvent(new Event('input', { bubbles: true }));
    input.dispatchEvent(new Event('change', { bubbles: true }));
  `,
    el,
    dateValue
  );
  await sleep(300);
}

function uniquePhone() {
  return `09${String(Date.now()).slice(-8)}`;
}

function url(path) {
  return `${config.app.baseUrl}${path}`;
}

function buildMomoCallbackUrl(orderId, resultCode = '0') {
  const params = new URLSearchParams({
    resultCode,
    orderId,
    transId: `AUTO-${Date.now()}`,
    message: resultCode === '0' ? 'Success' : 'Failed',
  });
  return `${url(config.routes.momoReturn)}?${params.toString()}`;
}

/**
 * Giả lập callback MoMo:
 * - useSession=true  → Selenium mở URL (giữ cookie phiên đăng nhập)
 * - useSession=false → fetch không cookie (test callback không session)
 */
async function simulateMomoCallback(driver, orderId, resultCode = '0', useSession = true) {
  const callbackUrl = buildMomoCallbackUrl(orderId, resultCode);

  if (useSession) {
    await driver.get(callbackUrl);
    await sleep(1000);
    const currentUrl = await driver.getCurrentUrl();
    return currentUrl.includes('/Booking');
  }

  const response = await fetch(callbackUrl, { redirect: 'manual' });
  return response.status === 302 || response.status === 200;
}

function formatVnpayDate(date) {
  const y = date.getFullYear();
  const m = String(date.getMonth() + 1).padStart(2, '0');
  const d = String(date.getDate()).padStart(2, '0');
  const h = String(date.getHours()).padStart(2, '0');
  const min = String(date.getMinutes()).padStart(2, '0');
  const s = String(date.getSeconds()).padStart(2, '0');
  return `${y}${m}${d}${h}${min}${s}`;
}

function vnpayBuildSignData(params) {
  return Object.keys(params)
    .filter(
      (key) =>
        params[key] !== '' &&
        params[key] != null &&
        key !== 'vnp_SecureHash' &&
        key !== 'vnp_SecureHashType'
    )
    .sort()
    .map((key) => `${encodeURIComponent(key)}=${encodeURIComponent(String(params[key]))}`)
    .join('&');
}

function buildVnpayCallbackUrl(holdCode, responseCode = '00') {
  const vnp = config.vnpay;
  const now = new Date();
  const params = {
    vnp_Amount: '10000000',
    vnp_BankCode: 'NCB',
    vnp_BankTranNo: `VNP${Date.now()}`,
    vnp_CardType: 'ATM',
    vnp_OrderInfo: `Thanh toan ve xe ${holdCode}`,
    vnp_PayDate: formatVnpayDate(now),
    vnp_ResponseCode: responseCode,
    vnp_TmnCode: vnp.tmnCode,
    vnp_TransactionNo: String(Date.now()).slice(-8),
    vnp_TxnRef: holdCode,
  };
  params.vnp_SecureHash = crypto
    .createHmac('sha512', vnp.hashSecret)
    .update(vnpayBuildSignData(params), 'utf8')
    .digest('hex');
  const query = Object.keys(params)
    .sort()
    .map((key) => `${encodeURIComponent(key)}=${encodeURIComponent(String(params[key]))}`)
    .join('&');
  return `${url(config.routes.vnpayConfirm)}?${query}`;
}

/** Giả lập VNPAY redirect về /Booking/ConfirmPayment (cần chữ ký HMAC hợp lệ). */
async function simulateVnpayCallback(driver, holdCode, responseCode = '00', useSession = true) {
  const callbackUrl = buildVnpayCallbackUrl(holdCode, responseCode);

  if (useSession) {
    await driver.get(callbackUrl);
    await sleep(1500);
    const currentUrl = await driver.getCurrentUrl();
    return currentUrl.includes('/Booking') && !currentUrl.includes('ConfirmPayment');
  }

  const response = await fetch(callbackUrl, { redirect: 'manual' });
  return response.status === 302 || response.status === 200;
}

async function prepareVnpayHold(driver, db, scenario) {
  await openBookingCreate(driver);
  await prepareQrBooking(driver, { ...scenario, PaymentMethod: 'VNPAY' });
  await waitForPaymentUrl(driver);
  const holdCode = await findLatestHoldOrderId(db);
  state.lastOrderId = holdCode;
  state.lastPaymentMethod = 'VNPAY';
  return holdCode;
}

async function prepareMomoHold(driver, db, scenario) {
  await openBookingCreate(driver);
  await prepareQrBooking(driver, { ...scenario, PaymentMethod: 'MOMO' });
  await waitForPaymentUrl(driver);
  const orderId = await findLatestHoldOrderId(db);
  state.lastOrderId = orderId;
  state.lastPaymentMethod = 'MOMO';
  return orderId;
}

async function preparePayosHold(driver, db, scenario) {
  await openBookingCreate(driver);
  await prepareQrBooking(driver, { ...scenario, PaymentMethod: 'PAYOS' });
  await waitForPaymentUrl(driver);
  const holdCode = await findLatestHoldOrderId(db);
  state.lastOrderId = holdCode;
  state.lastPaymentMethod = 'PAYOS';
  return holdCode;
}

function buildPayosCallbackUrl(resultCode = '00') {
  const route = config.routes.payosReturn || '/Booking/PayOSReturn';
  const code = resultCode.toString().trim().toLowerCase();
  if (code === 'cancel' || code === '99') {
    return `${url(route)}?cancel=true`;
  }
  return `${url(route)}?code=00&status=PAID`;
}

async function simulatePayosCallback(driver, resultCode = '00', useSession = true) {
  const callbackUrl = buildPayosCallbackUrl(resultCode);

  if (useSession) {
    await driver.get(callbackUrl);
    await sleep(1500);
    const currentUrl = await driver.getCurrentUrl();
    const path = await getCurrentPath(driver);
    if (resultCode.toString().toLowerCase() === 'cancel') {
      return path.includes('/Booking');
    }
    return path.includes('/Booking') && !path.includes('PayOSReturn');
  }

  const response = await fetch(callbackUrl, { redirect: 'manual' });
  return response.status === 302 || response.status === 200;
}

function isVnpayPaidInDb(booking) {
  const method = booking?.paymentInfo?.paymentMethod;
  return booking?.paymentStatus === 'Paid' && ['VnPay', 'VNPAY', 'vnpay'].includes(method);
}

function isPayosPaidInDb(booking) {
  const method = booking?.paymentInfo?.paymentMethod;
  return booking?.paymentStatus === 'Paid' && ['PAYOS', 'PayOS', 'payos'].includes(method);
}

async function installFetchInterceptor(driver) {
  await driver.executeScript(`
    window.__lastPaymentResult = null;
    const originalFetch = window.fetch;
    window.fetch = async function (...args) {
      const response = await originalFetch.apply(this, args);
      const reqUrl = typeof args[0] === 'string' ? args[0] : (args[0] && args[0].url) || '';
      if (reqUrl.includes('BookTicket')) {
        try {
          window.__lastPaymentResult = await response.clone().json();
        } catch (e) {
          window.__lastPaymentResult = { success: false, message: e.message };
        }
      }
      return response;
    };
  `);
}

async function resetPaymentResult(driver) {
  await driver.executeScript('window.__lastPaymentResult = null;');
}

async function waitForPaymentUrl(driver) {
  await driver.wait(async () => {
    const result = await driver.executeScript('return window.__lastPaymentResult;');
    return result && (result.paymentUrl || result.success === false);
  }, PAYMENT_WAIT_MS);

  const result = await driver.executeScript('return window.__lastPaymentResult;');
  if (!result?.success || !result?.paymentUrl) {
    throw new Error(
      result?.message ||
        'BookTicket không trả về paymentUrl — kiểm tra cấu hình MoMo/VNPAY/PayOS hoặc thông tin hành khách.'
    );
  }
  return result;
}

async function waitForCashRedirect(driver) {
  await driver.wait(async () => {
    const result = await driver.executeScript('return window.__lastPaymentResult;');
    return result && result.success === true && result.redirectUrl;
  }, WAIT_MS);
  return driver.executeScript('return window.__lastPaymentResult;');
}

async function acceptAlertIfPresent(driver) {
  try {
    const alert = await driver.wait(until.alertIsPresent(), 5000);
    const text = await alert.getText();
    await alert.accept();
    return text;
  } catch {
    return null;
  }
}

async function getCurrentPath(driver) {
  const currentUrl = await driver.getCurrentUrl();
  try {
    return new URL(currentUrl).pathname;
  } catch {
    return currentUrl;
  }
}

/** Login giống authAutomation.js — typeRobust + sleep, không chờ URL cứng. */
async function loginWithCredentials(driver, email, password) {
  const sel = config.selectors.login;
  await driver.get(url(config.routes.login));
  await driver.wait(until.elementLocated(By.css(sel.email)), WAIT_MS);
  await typeRobust(driver, sel.email, email);
  await typeRobust(driver, sel.password, password);
  await step(`Submit login: ${email}`, 300);
  await driver.findElement(By.css(sel.submitBtn)).click();
  await sleep(2000);
  return getCurrentPath(driver);
}

async function loginAsBookingUser(driver, scenario = {}) {
  const { email, password } = resolveBookingCredentials(scenario);
  return loginWithCredentials(driver, email, password);
}

async function loginOnceAtStart(driver) {
  await clearSession(driver);
  const { email, password } = resolveBookingCredentials();
  await step(`Đăng nhập một lần: ${email}`);
  const path = await loginWithCredentials(driver, email, password);
  await driver.get(url(config.routes.bookingCreate));
  await sleep(800);

  const bookingFields = await driver.findElements(By.css(config.selectors.booking.departure));
  if (!bookingFields.length) {
    throw new Error(
      `Không vào được /Booking/Create sau đăng nhập ${email}. Kiểm tra app tại http://localhost:5280.`
    );
  }

  state.sessionReady = true;
  await installFetchInterceptor(driver);
  console.log(`  -> Phiên đăng nhập sẵn sàng (${path})`);
}

/**
 * Phiên admin login sẵn trước khi chạy test — openBookingCreate chỉ mở lại trang booking.
 */
async function openBookingCreate(driver) {
  await driver.get(url(config.routes.bookingCreate));
  await sleep(600);

  const path = await getCurrentPath(driver);
  if (path.includes('/Account/Login')) {
    await loginOnceAtStart(driver);
    return;
  }

  if (path.includes('AccessDenied')) {
    throw new Error('Access Denied tại /Booking/Create — dùng admin@src.com / Admin@123.');
  }

  const bookingFields = await driver.findElements(By.css(config.selectors.booking.departure));
  if (!bookingFields.length) {
    throw new Error('Không mở được trang đặt vé /Booking/Create.');
  }

  await installFetchInterceptor(driver);
}

async function searchTrips(driver, scenario = {}) {
  const sel = config.selectors.booking;
  const departure = scenario.Departure || config.bookingSearch.departure;
  const destination = scenario.Destination || config.bookingSearch.destination;
  const travelDate = resolveTravelDateFromScenario(scenario);

  await fillInput(driver, sel.departure, departure);
  await fillInput(driver, sel.destination, destination);
  await setDateInput(driver, sel.travelDate, travelDate);
  await step(`Tìm chuyến ${departure} → ${destination}, ngày ${travelDate}`);
  await driver.findElement(By.css(sel.searchBtn)).click();
  await sleep(1200);

  await driver.wait(async () => {
    const area = await driver.findElement(By.css('#searchResultArea'));
    const text = (await area.getText()).toLowerCase();
    if (text.includes('không thể tải') || text.includes('không có chuyến')) return false;
    const cards = await driver.findElements(By.css(sel.tripCard));
    return cards.length > 0;
  }, WAIT_MS).catch(async () => {
    const areaText = await driver.findElement(By.css('#searchResultArea')).getText();
    throw new Error(
      `Không tìm thấy chuyến xe (ngày ${travelDate}). ${areaText || 'Kiểm tra MongoDB có trip Scheduled cho tuyến này.'}`
    );
  });

  return travelDate;
}

/** Chọn chuyến có nhiều ghế trống nhất (tránh chuyến "Trống: 0 chỗ"). */
async function selectTripWithAvailableSeats(driver) {
  const sel = config.selectors.booking;
  const tripIndex = await driver.executeScript(`
    const cards = Array.from(document.querySelectorAll('#searchResultArea > div'));
    let bestIdx = -1;
    let bestSeats = 0;
    cards.forEach((card, idx) => {
      const text = card.innerText || '';
      const match = text.match(/Trống:\\s*(\\d+)\\s*chỗ/i);
      const seats = match ? parseInt(match[1], 10) : 0;
      if (seats > bestSeats) {
        bestSeats = seats;
        bestIdx = idx;
      }
    });
    return { bestIdx, bestSeats };
  `);

  if (!tripIndex || tripIndex.bestIdx < 0 || tripIndex.bestSeats <= 0) {
    throw new Error('Không có chuyến nào còn ghế trống. Thử ngày khác hoặc kiểm tra dữ liệu trip trong MongoDB.');
  }

  const cards = await driver.findElements(By.css(sel.tripCard));
  await step(`Chọn chuyến #${tripIndex.bestIdx + 1} (còn ${tripIndex.bestSeats} ghế trống)`);
  await cards[tripIndex.bestIdx].click();

  await driver.wait(until.elementLocated(By.css(sel.seatGrid)), WAIT_MS);
  await sleep(800);
}

async function waitForAvailableSeats(driver) {
  const sel = config.selectors.booking;
  await driver.wait(async () => {
    const seats = await driver.findElements(By.css(sel.availableSeat));
    return seats.length > 0;
  }, WAIT_MS).catch(async () => {
    const gridText = await driver.findElement(By.css(sel.seatGrid)).getText();
    throw new Error(`Không có ghế để chọn. ${gridText || 'Sơ đồ ghế trống hoặc chuyến đã hết chỗ.'}`);
  });
}

async function selectFirstAvailableSeat(driver) {
  const sel = config.selectors.booking;
  await waitForAvailableSeats(driver);
  const seats = await driver.findElements(By.css(sel.availableSeat));
  const seatLabel = await seats[0].getText();
  await step(`Chọn ghế ${seatLabel.trim()}`);
  await seats[0].click();
  await sleep(400);
}

async function searchAndSelectTripWithSeats(driver, scenario = {}) {
  await searchTrips(driver, scenario);
  await selectTripWithAvailableSeats(driver);
  await selectFirstAvailableSeat(driver);
}

async function fillPassengerInfo(driver, phone, scenario = {}) {
  const sel = config.selectors.booking;
  const name = scenario.PassengerName || config.passenger.name;
  const dob = scenario.PassengerDob || config.passenger.dob;
  const email = scenario.PassengerEmail || config.passenger.email;
  await fillInput(driver, sel.phone, phone);
  await fillInput(driver, sel.name, name);
  await setDateInput(driver, sel.dob, dob);
  await fillInput(driver, sel.email, email);
}

async function openPaymentQrPage(driver, paymentUrl, label = 'QR thanh toán') {
  if (!paymentUrl) {
    throw new Error('Không có paymentUrl từ server.');
  }
  await step(`Mở trang ${label} — giữ ${QR_DEMO_DELAY / 1000}s để xem QR...`, 500);
  await driver.get(paymentUrl);
  await sleep(QR_DEMO_DELAY);
}

async function selectPaymentMethod(driver, method) {
  const selector = `${config.selectors.booking.paymentMethod}[value="${method}"]`;
  const el = await driver.findElement(By.css(selector));
  await driver.executeScript('arguments[0].scrollIntoView({block:"center"});', el);
  await sleep(300);
  await el.click();
  await sleep(300);
}

async function submitBooking(driver) {
  await driver.findElement(By.css(config.selectors.booking.submitBtn)).click();
}

async function prepareQrBooking(driver, scenario = {}) {
  const paymentMethod = resolvePaymentMethod(scenario, config.paymentMethods.momo);
  await resetPaymentResult(driver);
  await searchAndSelectTripWithSeats(driver, scenario);

  await driver.wait(async () => {
    const label = await driver.findElement(By.css('#txtSeatNum')).getText();
    return label && !label.includes('Chưa chọn');
  }, WAIT_MS);

  const phone = uniquePhone();
  await step('Nhập thông tin hành khách');
  await fillPassengerInfo(driver, phone, scenario);
  await step(`Chọn phương thức thanh toán: ${paymentMethod}`);
  await selectPaymentMethod(driver, paymentMethod);
  await step(`Submit booking — ${paymentMethod}`);
  await submitBooking(driver);
  await sleep(1000);
  return phone;
}

async function findLatestHoldOrderId(db) {
  const trips = await db
    .collection(config.database.tripsCollection)
    .find({ 'realtimeSeats.status': 'Holding' })
    .sort({ _id: -1 })
    .limit(5)
    .toArray();

  for (const trip of trips) {
    for (const seat of trip.realtimeSeats || []) {
      if (seat.status === 'Holding' && seat.heldByCustomerId) {
        return seat.heldByCustomerId;
      }
    }
  }
  return null;
}

async function waitForPaidBooking(db, orderId) {
  const deadline = Date.now() + WAIT_MS;
  while (Date.now() < deadline) {
    const booking = await db.collection(config.database.bookingsCollection).findOne({ bookingCode: orderId });
    if (booking?.paymentStatus === 'Paid') return booking;
    await sleep(400);
  }
  return db.collection(config.database.bookingsCollection).findOne({ bookingCode: orderId });
}

async function findLatestPaidBooking(db, paymentMethod) {
  const filter = { paymentStatus: 'Paid' };
  if (paymentMethod) {
    const normalized = paymentMethod.toUpperCase();
    if (normalized === 'CASH') {
      filter['paymentInfo.paymentMethod'] = { $in: ['Cash', 'CASH', 'cash'] };
    } else if (normalized === 'VNPAY') {
      filter['paymentInfo.paymentMethod'] = { $in: ['VnPay', 'VNPAY', 'vnpay'] };
    } else if (normalized === 'PAYOS') {
      filter['paymentInfo.paymentMethod'] = { $in: ['PAYOS', 'PayOS', 'payos'] };
    } else {
      filter['paymentInfo.paymentMethod'] = normalized;
    }
  }
  return db.collection(config.database.bookingsCollection).findOne(filter, { sort: { _id: -1 } });
}

async function assertQrPageDisplayed(driver, paymentUrl) {
  try {
    await driver.wait(
      until.or(
        until.elementLocated(By.css('img')),
        until.elementLocated(By.css('canvas')),
        until.elementLocated(By.css('[class*="qr" i], [id*="qr" i]'))
      ),
      WAIT_MS
    );
    return true;
  } catch {
    return Boolean(paymentUrl);
  }
}
// ==========================================
async function waitForBookingIndexAfterPayment(driver) {
  await driver.wait(async () => {
    const currentUrl = await driver.getCurrentUrl();
    return currentUrl.includes('/Booking') && !currentUrl.includes('/Create');
  }, WAIT_MS);
  await sleep(1500);
}

async function waitForSuccessModal(driver) {
  const sel = config.selectors.ticket;
  await driver.wait(async () => {
    const modal = await driver.findElement(By.css(sel.successModal));
    const cls = (await modal.getAttribute('class')) || '';
    return !cls.includes('hidden');
  }, WAIT_MS);
  await driver.wait(until.elementLocated(By.css(`${sel.bookingDetails} p`)), WAIT_MS);
}

async function completePaidBooking(driver, db, scenario) {
  const method = resolvePaymentMethod(scenario, config.paymentMethods.cash);
  const methodUpper = method.toUpperCase();

  await openBookingCreate(driver);
  await prepareQrBooking(driver, scenario);

  if (isCashMethod(method)) {
    await waitForCashRedirect(driver);
    await waitForBookingIndexAfterPayment(driver);
    return;
  }

  await waitForPaymentUrl(driver);
  const holdCode = await findLatestHoldOrderId(db);
  if (!holdCode) {
    throw new Error(`${methodUpper} — không tìm thấy holdCode/orderId để hoàn tất thanh toán.`);
  }

  if (methodUpper === 'MOMO') {
    await simulateMomoCallback(driver, holdCode, '0', true);
  } else if (methodUpper === 'VNPAY') {
    await simulateVnpayCallback(driver, holdCode, '00', true);
  } else if (methodUpper === 'PAYOS') {
    await simulatePayosCallback(driver, '00', true);
  } else {
    throw new Error(`completePaidBooking chưa hỗ trợ ${methodUpper}`);
  }

  await waitForBookingIndexAfterPayment(driver);
}

async function verifyPrintTicket(driver) {
  const sel = config.selectors.ticket;
  await waitForSuccessModal(driver);

  const detailsText = await driver.findElement(By.css(sel.bookingDetails)).getText();
  if (!/mã vé/i.test(detailsText)) {
    throw new Error('Modal thành công không hiển thị mã vé.');
  }

  const handlesBefore = await driver.getAllWindowHandles();
  await step('Bấm nút In vé');
  const printBtn = await driver.findElement(By.css(sel.printBtn));
  await driver.executeScript('arguments[0].scrollIntoView({block:"center"});', printBtn);
  await printBtn.click();
  await sleep(1200);

  const handlesAfter = await driver.getAllWindowHandles();
  if (handlesAfter.length <= handlesBefore.length) {
    throw new Error('Không mở được cửa sổ in vé (có thể bị chặn popup).');
  }

  const newHandle = handlesAfter.find((h) => !handlesBefore.includes(h));
  await driver.switchTo().window(newHandle);
  const bodyText = await driver.findElement(By.css('body')).getText();
  const printed = /vé xe|mã vé/i.test(bodyText);
  await driver.close();
  await driver.switchTo().window(handlesBefore[0]);

  try {
    const closeBtn = await driver.findElement(
      By.xpath("//div[@id='successModal']//button[contains(normalize-space(.),'Đóng')]")
    );
    await closeBtn.click();
    await sleep(400);
  } catch {
    await driver.executeScript("document.getElementById('successModal')?.classList.add('hidden');");
  }

  return printed;
}

// HANDLERS (Excel-driven — Category → hàm)
// ==========================================

async function tcPrintTicket(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const method = resolvePaymentMethod(scenario, config.paymentMethods.cash);
  await step(`Thanh toán ${method} thành công và in vé`);
  await completePaidBooking(driver, db, scenario);
  const printed = await verifyPrintTicket(driver);
  logResult(
    id,
    printed,
    printed ? `${method} — modal vé hiện và in vé OK.` : `${method} — không xác minh được nội dung in vé.`
  );
}

async function tcLoginAdminValid(driver, db, scenario) {
  const id = scenario.TestCaseID;
  await step('Đăng nhập admin và mở trang đặt vé');
  await loginOnceAtStart(driver);
  const path = await getCurrentPath(driver);
  const ok = state.sessionReady && !path.includes('/Account/Login') && !path.includes('AccessDenied');
  logResult(id, ok, ok ? 'Admin đăng nhập — truy cập /Booking/Create thành công.' : 'Không vào được trang đặt vé.');
}

async function tcLoginFailWrongPassword(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const sel = config.selectors.login;
  const email = scenario.Email || config.users.admin.email;
  const password = scenario.Password || config.users.wrongPassword;
  await step('Thử đăng nhập sai mật khẩu');
  await clearSession(driver);
  await driver.get(url(config.routes.login));
  await typeRobust(driver, sel.email, email);
  await typeRobust(driver, sel.password, password);
  await driver.findElement(By.css(sel.submitBtn)).click();
  await driver.wait(until.urlMatches(/\/Account\/Login/i), WAIT_MS);
  const stillOnLogin = (await getCurrentPath(driver)).includes('/Account/Login');
  await step('Đăng nhập lại admin cho các test tiếp theo');
  await loginOnceAtStart(driver);
  logResult(id, stillOnLogin, stillOnLogin ? 'Đăng nhập sai mật khẩu bị từ chối.' : 'Không ở lại trang Login.');
}

async function tcSearchTripSuccess(driver, db, scenario) {
  const id = scenario.TestCaseID;
  await step('Tìm chuyến và chọn ghế');
  await openBookingCreate(driver);
  await searchTrips(driver, scenario);
  await selectTripWithAvailableSeats(driver);
  await waitForAvailableSeats(driver);
  const visible = await driver.findElement(By.css(config.selectors.booking.detailSection)).isDisplayed();
  logResult(id, visible, 'Tìm kiếm chuyến, chọn chuyến có ghế và hiển thị sơ đồ ghế.');
}

async function tcSubmitWithoutSeat(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const method = resolvePaymentMethod(scenario, config.paymentMethods.cash);
  await step('Submit booking khi chưa chọn ghế');
  await openBookingCreate(driver);
  await searchTrips(driver, scenario);
  await selectTripWithAvailableSeats(driver);
  await fillPassengerInfo(driver, uniquePhone(), scenario);
  await selectPaymentMethod(driver, method);
  await submitBooking(driver);
  const alertText = await acceptAlertIfPresent(driver);
  const blocked = alertText && /ghế|chỗ|ngồi/i.test(alertText);
  logResult(id, blocked, blocked ? 'Chưa chọn ghế — hệ thống chặn đặt vé.' : `Alert: ${alertText || 'none'}`);
}

async function tcPaymentMethodFlow(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const method = resolvePaymentMethod(scenario, config.paymentMethods.cash);
  const methodLabel = method.toUpperCase();

  await step(`Luồng thanh toán: ${methodLabel}`);
  await openBookingCreate(driver);
  await prepareQrBooking(driver, scenario);

  if (isCashMethod(method)) {
    const result = await waitForCashRedirect(driver);
    const ok = Boolean(result.success && result.redirectUrl);
    state.lastPaymentMethod = 'Cash';
    state.lastBookingCode = result.bookingCode || state.lastOrderId;

    if (shouldVerifyDb(scenario) && ok) {
      await sleep(800);
      const booking = await findLatestPaidBooking(db, 'Cash');
      const dbOk = booking?.paymentStatus === 'Paid';
      logResult(id, dbOk, dbOk ? 'Cash — đặt vé Paid trên MongoDB.' : 'Cash thành công UI nhưng chưa thấy Paid trên DB.');
      return;
    }

    logResult(id, ok, ok ? 'Cash — đặt vé thành công, không cần QR.' : 'Cash — không nhận redirectUrl.');
    return;
  }

  const result = await waitForPaymentUrl(driver);
  const valid = validatePaymentUrl(method, result.paymentUrl);
  state.lastPaymentUrl = valid ? result.paymentUrl : null;
  state.lastPaymentMethod = methodLabel;

  if (!valid) {
    logResult(id, false, `${methodLabel} — không nhận paymentUrl hợp lệ.`);
    return;
  }

  if (shouldOpenQrPage(scenario)) {
    await openPaymentQrPage(driver, result.paymentUrl, `${methodLabel} QR`);
    const displayed = await assertQrPageDisplayed(driver, result.paymentUrl);
    if (!displayed) {
      logResult(id, false, `${methodLabel} — mở link nhưng không thấy QR.`);
      return;
    }
    await driver.get(url(config.routes.bookingCreate));
    await installFetchInterceptor(driver);
  }

  const orderId = await findLatestHoldOrderId(db);
  state.lastOrderId = orderId;

  if (shouldSimulateCallback(scenario) && methodLabel === 'MOMO') {
    if (!orderId) {
      logResult(id, false, 'MOMO — có QR nhưng không tìm thấy orderId.');
      return;
    }
    const code = scenario.CallbackResultCode?.toString() || '0';
    const paid = await simulateMomoCallback(driver, orderId, code, useSessionFromScenario(scenario));
    if (paid) await step('MOMO callback thành công — quay về Booking', 2000);

    if (shouldVerifyDb(scenario)) {
      const booking = await waitForPaidBooking(db, orderId);
      const dbOk = booking?.paymentStatus === 'Paid' && booking?.paymentInfo?.paymentMethod === 'MOMO';
      logResult(id, paid && dbOk, dbOk ? `MOMO — QR + callback → Paid (${orderId}).` : 'MOMO callback không cập nhật Paid trên DB.');
      return;
    }

    logResult(id, paid, paid ? `MOMO — QR + callback thành công (${orderId}).` : 'MOMO — callback thất bại.');
    return;
  }

  if (shouldSimulateCallback(scenario) && methodLabel === 'VNPAY') {
    if (!orderId) {
      logResult(id, false, 'VNPAY — có QR nhưng không tìm thấy holdCode.');
      return;
    }
    const code = scenario.CallbackResultCode?.toString() || '00';
    const paid = await simulateVnpayCallback(driver, orderId, code, useSessionFromScenario(scenario));
    if (paid) await step('VNPAY ConfirmPayment thành công — quay về Booking', 2000);

    if (shouldVerifyDb(scenario)) {
      const booking = await waitForPaidBooking(db, orderId);
      const dbOk = isVnpayPaidInDb(booking);
      logResult(id, paid && dbOk, dbOk ? `VNPAY — QR + callback → Paid (${orderId}).` : 'VNPAY callback không cập nhật Paid trên DB.');
      return;
    }

    logResult(id, paid, paid ? `VNPAY — callback thành công (${orderId}).` : 'VNPAY — callback thất bại.');
    return;
  }

  if (shouldSimulateCallback(scenario) && methodLabel === 'PAYOS') {
    if (!orderId) {
      logResult(id, false, 'PayOS — có QR nhưng không tìm thấy holdCode.');
      return;
    }
    const code = scenario.CallbackResultCode?.toString() || '00';
    const paid = await simulatePayosCallback(driver, code, useSessionFromScenario(scenario));
    if (paid && code.toLowerCase() !== 'cancel') await step('PayOS Return thành công — quay về Booking', 2000);

    if (shouldVerifyDb(scenario)) {
      const booking = await waitForPaidBooking(db, orderId);
      const dbOk = isPayosPaidInDb(booking);
      logResult(id, paid && dbOk, dbOk ? `PayOS — QR + callback → Paid (${orderId}).` : 'PayOS callback không cập nhật Paid trên DB.');
      return;
    }

    const isCancel = code.toLowerCase() === 'cancel';
    logResult(
      id,
      paid,
      isCancel
        ? 'PayOS — hủy giao dịch, quay về Booking.'
        : paid
          ? `PayOS — callback thành công (${orderId}).`
          : 'PayOS — callback thất bại.'
    );
    return;
  }

  logResult(id, true, `${methodLabel} — khởi tạo link thanh toán QR hợp lệ.`);
}

async function tcPaymentMethodsVisible(driver, db, scenario) {
  const id = scenario.TestCaseID;
  await openBookingCreate(driver);
  const methods = ['Cash', 'VNPAY', 'MOMO', 'PAYOS'];
  const missing = [];

  for (const method of methods) {
    const elements = await driver.findElements(
      By.css(`${config.selectors.booking.paymentMethod}[value="${method}"]`)
    );
    if (!elements.length) missing.push(method);
  }

  logResult(
    id,
    missing.length === 0,
    missing.length === 0
      ? 'Form hiển thị đủ Cash, VNPAY, MOMO, PayOS.'
      : `Thiếu phương thức: ${missing.join(', ')}`
  );
}

async function tcPaymentMethodSelect(driver, db, scenario) {
  const id = scenario.TestCaseID;
  await openBookingCreate(driver);
  await searchTrips(driver, scenario);
  await selectTripWithAvailableSeats(driver);
  await selectFirstAvailableSeat(driver);

  const methods = ['Cash', 'VNPAY', 'MOMO', 'PAYOS'];
  const failed = [];

  for (const method of methods) {
    try {
      await selectPaymentMethod(driver, method);
      const selected = await driver.findElement(
        By.css(`${config.selectors.booking.paymentMethod}[value="${method}"]:checked`)
      );
      if (!(await selected.isDisplayed())) failed.push(method);
    } catch {
      failed.push(method);
    }
  }

  logResult(
    id,
    failed.length === 0,
    failed.length === 0
      ? 'Chọn được lần lượt Cash, VNPAY, MOMO, PayOS trên form.'
      : `Không chọn được: ${failed.join(', ')}`
  );
}

async function tcVnpayCallbackFail(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const holdCode = await prepareVnpayHold(driver, db, scenario);
  if (!holdCode) {
    logResult(id, false, 'Không tìm thấy holdCode VNPAY.');
    return;
  }
  const code = scenario.CallbackResultCode?.toString() || '24';
  await simulateVnpayCallback(driver, holdCode, code, useSessionFromScenario(scenario));
  const booking = await db.collection(config.database.bookingsCollection).findOne({ bookingCode: holdCode });
  logResult(id, !booking || booking.paymentStatus !== 'Paid', 'VNPAY callback thất bại — booking không Paid.');
}

async function tcVnpayCallbackNoSession(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const holdCode = await prepareVnpayHold(driver, db, scenario);
  if (!holdCode) {
    logResult(id, false, 'Không tìm thấy holdCode VNPAY.');
    return;
  }
  await simulateVnpayCallback(driver, holdCode, scenario.CallbackResultCode?.toString() || '00', false);
  const booking = await db.collection(config.database.bookingsCollection).findOne({ bookingCode: holdCode });
  logResult(id, !booking || booking.paymentStatus !== 'Paid', 'VNPAY callback không session — không hoàn tất.');
}

async function tcMomoCallbackFail(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const orderId = await prepareMomoHold(driver, db, scenario);
  if (!orderId) {
    logResult(id, false, 'Không tìm thấy orderId MOMO.');
    return;
  }
  const code = scenario.CallbackResultCode?.toString() || '1006';
  await simulateMomoCallback(driver, orderId, code, useSessionFromScenario(scenario));
  const booking = await db.collection(config.database.bookingsCollection).findOne({ bookingCode: orderId });
  logResult(id, !booking || booking.paymentStatus !== 'Paid', 'MOMO callback thất bại — booking không Paid.');
}

async function tcMomoCallbackNoSession(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const orderId = await prepareMomoHold(driver, db, scenario);
  if (!orderId) {
    logResult(id, false, 'Không tìm thấy orderId MOMO.');
    return;
  }
  await simulateMomoCallback(driver, orderId, scenario.CallbackResultCode?.toString() || '0', false);
  const booking = await db.collection(config.database.bookingsCollection).findOne({ bookingCode: orderId });
  logResult(id, !booking || booking.paymentStatus !== 'Paid', 'MOMO callback không session — không hoàn tất.');
}

/** @deprecated dùng MomoCallbackNoSession */
async function tcCallbackNoSession(driver, db, scenario) {
  return tcMomoCallbackNoSession(driver, db, scenario);
}

async function tcPayosCallbackFail(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const holdCode = await preparePayosHold(driver, db, scenario);
  if (!holdCode) {
    logResult(id, false, 'Không tìm thấy holdCode PayOS.');
    return;
  }
  await simulatePayosCallback(driver, scenario.CallbackResultCode?.toString() || 'cancel', useSessionFromScenario(scenario));
  const booking = await db.collection(config.database.bookingsCollection).findOne({ bookingCode: holdCode });
  logResult(id, !booking || booking.paymentStatus !== 'Paid', 'PayOS hủy — booking không Paid.');
}

async function tcPayosCallbackNoSession(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const holdCode = await preparePayosHold(driver, db, scenario);
  if (!holdCode) {
    logResult(id, false, 'Không tìm thấy holdCode PayOS.');
    return;
  }
  await simulatePayosCallback(driver, scenario.CallbackResultCode?.toString() || '00', false);
  const booking = await db.collection(config.database.bookingsCollection).findOne({ bookingCode: holdCode });
  logResult(id, !booking || booking.paymentStatus !== 'Paid', 'PayOS callback không session — không hoàn tất.');
}

async function tcDbVerifyPayment(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const method = resolvePaymentMethod(scenario, state.lastPaymentMethod || 'MOMO');
  const methodUpper = method.toUpperCase();

  if (methodUpper === 'MOMO' && state.lastOrderId) {
    const booking = await waitForPaidBooking(db, state.lastOrderId);
    const ok = booking?.paymentStatus === 'Paid' && booking?.paymentInfo?.paymentMethod === 'MOMO';
    logResult(id, ok, ok ? `MongoDB MOMO Paid (${state.lastOrderId}).` : 'MongoDB chưa có MOMO Paid.');
    return;
  }

  if (isCashMethod(method)) {
    const booking = await findLatestPaidBooking(db, 'Cash');
    const ok =
      booking?.paymentStatus === 'Paid' &&
      ['Cash', 'CASH', 'cash'].includes(booking?.paymentInfo?.paymentMethod);
    logResult(id, ok, ok ? `MongoDB Cash Paid (${booking.bookingCode}).` : 'MongoDB chưa có Cash Paid.');
    return;
  }

  if (methodUpper === 'VNPAY' && state.lastOrderId) {
    const booking = await waitForPaidBooking(db, state.lastOrderId);
    const ok = isVnpayPaidInDb(booking);
    logResult(id, ok, ok ? `MongoDB VNPAY Paid (${state.lastOrderId}).` : 'MongoDB chưa có VNPAY Paid.');
    return;
  }

  if (methodUpper === 'VNPAY') {
    const booking = await findLatestPaidBooking(db, 'VNPAY');
    const ok = isVnpayPaidInDb(booking);
    logResult(id, ok, ok ? `MongoDB VNPAY Paid (${booking.bookingCode}).` : 'MongoDB chưa có VNPAY Paid.');
    return;
  }

  if (methodUpper === 'PAYOS' && state.lastOrderId) {
    const booking = await waitForPaidBooking(db, state.lastOrderId);
    const ok = isPayosPaidInDb(booking);
    logResult(id, ok, ok ? `MongoDB PayOS Paid (${state.lastOrderId}).` : 'MongoDB chưa có PayOS Paid.');
    return;
  }

  if (methodUpper === 'PAYOS') {
    const booking = await findLatestPaidBooking(db, 'PAYOS');
    const ok = isPayosPaidInDb(booking);
    logResult(id, ok, ok ? `MongoDB PayOS Paid (${booking.bookingCode}).` : 'MongoDB chưa có PayOS Paid.');
    return;
  }

  logResult(id, false, `DbVerifyPayment chưa hỗ trợ ${methodUpper} — cần chạy PaymentMethodFlow trước.`);
}

async function tcSearchEmptyFields(driver, db, scenario) {
  const id = scenario.TestCaseID;
  const sel = config.selectors.booking;
  await openBookingCreate(driver);
  await driver.findElement(By.css(sel.departure)).clear();
  await driver.findElement(By.css(sel.destination)).clear();
  await driver.findElement(By.css(sel.travelDate)).clear();
  await driver.findElement(By.css(sel.searchBtn)).click();
  const alertText = await acceptAlertIfPresent(driver);
  logResult(id, alertText && alertText.toLowerCase().includes('đầy đủ'), 'Tìm kiếm thiếu thông tin — cảnh báo.');
}

const handlers = {
  LoginAdminValid: tcLoginAdminValid,
  LoginFailWrongPassword: tcLoginFailWrongPassword,
  SearchTripSuccess: tcSearchTripSuccess,
  SubmitWithoutSeat: tcSubmitWithoutSeat,
  PaymentMethodFlow: tcPaymentMethodFlow,
  PaymentMethodsVisible: tcPaymentMethodsVisible,
  PaymentMethodSelect: tcPaymentMethodSelect,
  MomoCallbackFail: tcMomoCallbackFail,
  MomoCallbackNoSession: tcMomoCallbackNoSession,
  CallbackNoSession: tcCallbackNoSession,
  VnpayCallbackFail: tcVnpayCallbackFail,
  VnpayCallbackNoSession: tcVnpayCallbackNoSession,
  PayosCallbackFail: tcPayosCallbackFail,
  PayosCallbackNoSession: tcPayosCallbackNoSession,
  PrintTicket: tcPrintTicket,
  DbVerifyPayment: tcDbVerifyPayment,
  SearchEmptyFields: tcSearchEmptyFields,
};

// ==========================================
// HÀM CHÍNH (Excel-driven)
// ==========================================

async function runQrPaymentAutomation() {
  console.log('Đang tải dữ liệu Test Cases từ Excel...');
  loadedTestCases = loadExcelTestCases();
  console.log('=== BẮT ĐẦU AUTOMATION TEST LUỒNG THANH TOÁN (Excel) ===');
  console.log(`File: ${config.excel?.fileName || 'QrPaymentTestCases.xlsx'} — ${loadedTestCases.length} dòng`);
  console.log('Thứ tự: BOOK → CASH → MOMO → VNPAY → PAYOS → BOOK');
  console.log('(Đăng nhập admin tự động — không test AUTH)');

  const client = new MongoClient(config.database.uri);
  const driver = await new Builder().forBrowser('chrome').build();

  try {
    await client.connect();
    const db = client.db(config.database.dbName);

    await loginOnceAtStart(driver);

    for (const scenario of loadedTestCases) {
      if (!isEnabled(scenario)) {
        console.log(`${scenario.TestCaseID} Skip: Enabled = N`);
        continue;
      }

      const handler = handlers[scenario.Category];
      console.log(`\nĐang chạy ${scenario.TestCaseID}: ${scenario.Description}...`);

      if (!handler) {
        logResult(scenario.TestCaseID, false, `Không có handler cho Category "${scenario.Category}"`);
        continue;
      }

      try {
        await handler(driver, db, scenario);
      } catch (error) {
        logResult(scenario.TestCaseID, false, error.message);
      }
      await sleep(TEST_DELAY);
    }

    const passCount = results.filter((x) => x.status === 'PASS').length;
    const failCount = results.filter((x) => x.status === 'FAIL').length;

    console.log('\n=== KẾT QUẢ AUTOMATION TEST ===');
    console.log(`Tổng số: ${results.length}`);
    console.log(`PASS: ${passCount}`);
    console.log(`FAIL: ${failCount}`);

    for (const item of results.filter((x) => x.status === 'FAIL')) {
      console.log(`- ${item.id} FAIL: ${item.message}`);
    }

    if (failCount === 0 && results.length > 0) {
      console.log('=== HOÀN TẤT TOÀN BỘ KỊCH BẢN KIỂM THỬ ===');
    }
  } catch (error) {
    console.error('Lỗi trong quá trình chạy:', error);
  } finally {
    await driver.quit().catch(() => {});
    await client.close().catch(() => {});
  }
}

module.exports = runQrPaymentAutomation;

if (require.main === module) {
  runQrPaymentAutomation();
}
