const { Builder, By, until, Key } = require('selenium-webdriver');
const { MongoClient } = require('mongodb');
const xlsx = require('xlsx');

// Đã sửa thành gọi file config.js chuẩn chỉnh
const config = require('./config');
const sleep = (ms) => new Promise(resolve => setTimeout(resolve, ms));

// ==========================================
// HÀM HỖ TRỢ: NHẬP TEXT
// ==========================================
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

// ==========================================
// HÀM HỖ TRỢ: CHUYÊN TRỊ DATE PICKER
// ==========================================
async function setDatePicker(driver, selector, dateValue) {
    const el = await driver.findElement(By.css(selector));

    if (dateValue && dateValue.toString().trim() !== '') {
        const dateStr = dateValue.toString().trim();
        await driver.executeScript("arguments[0].removeAttribute('readonly');", el);

        await driver.executeScript(`
            let input = arguments[0];
            let date = arguments[1];
            
            if (input._flatpickr) {
                input._flatpickr.setDate(date, true);
            } else if (window.jQuery && window.jQuery(input).data('datepicker')) {
                window.jQuery(input).datepicker('setDate', date);
            } else {
                input.value = date;
                input.dispatchEvent(new Event('input', { bubbles: true }));
                input.dispatchEvent(new Event('change', { bubbles: true }));
            }
        `, el, dateStr);

        await el.sendKeys(Key.ENTER, Key.TAB);
    } else {
        await driver.executeScript(`
            let input = arguments[0];
            if (input._flatpickr) input._flatpickr.clear(); 
            input.removeAttribute('readonly');
            input.value = '';
            input.dispatchEvent(new Event('change', { bubbles: true }));
        `, el);
        await el.sendKeys(Key.TAB);
    }
    await sleep(500);
}

// ==========================================
// HÀM HỖ TRỢ: CHỌN DROPDOWN BẰNG CLICK THẬT
// ==========================================
async function selectDropdownByClick(driver, selectSelector, targetText = 'TicketAgent') {
    try {
        const selectEl = await driver.findElement(By.css(selectSelector));
        await selectEl.click();
        await sleep(500);

        const optionXpath = `//option[contains(text(), '${targetText}')]`;
        const optionEl = await driver.findElement(By.xpath(optionXpath));
        await optionEl.click();
        await sleep(600);
    } catch (e) {
        await driver.executeScript(`
            let select = document.querySelector('${selectSelector}');
            if (select) {
                for (let i = 0; i < select.options.length; i++) {
                    if (select.options[i].text.includes('${targetText}')) {
                        select.selectedIndex = i;
                        select.dispatchEvent(new Event('change', { bubbles: true }));
                        break;
                    }
                }
            }
        `);
        await sleep(600);
    }
}

// ==========================================
// HÀM CHÍNH
// ==========================================
async function runAuthAutomation() {
    console.log("Đang tải dữ liệu Test Cases từ Excel...");
    const workbook = xlsx.readFile('TestCases.xlsx', { cellDates: true });
    const sheet = workbook.Sheets[workbook.SheetNames[0]];
    const excelData = xlsx.utils.sheet_to_json(sheet, { defval: '' });

    const loginFailures = excelData.filter(row => row.Category === 'LoginFail');
    const adminLogin = excelData.find(row => row.Category === 'LoginValid');
    const createFailures = excelData.filter(row => row.Category === 'CreateFail');
    const createSuccess = excelData.find(row => row.Category === 'CreateValid');

    // Đã sửa đồng bộ biến cấu hình DB theo config.js (config.db.uri)
    const client = new MongoClient(config.db.uri);
    const driver = await new Builder().forBrowser('chrome').build();

    try {
        await client.connect();
        // Đã sửa cấu hình DB (config.db.name, config.db.collection)
        const userCollection = client.db(config.db.name).collection(config.db.collection);

        const uniqueId = Date.now();
        const empEmail = `employee_${uniqueId}@gmail.com`;
        const empPass = createSuccess ? createSuccess.Password : 'Employee@123';

        console.log("=== BẮT ĐẦU CHẠY AUTOMATION TEST EXCEL-DRIVEN ===");

        for (const scenario of loginFailures) {
            console.log(`Đang chạy ${scenario.TestCaseID}: ${scenario.Description}...`);
            await driver.get(config.app.baseUrl + '/Account/Login');
            await typeRobust(driver, config.selectors.login.email, scenario.Email);
            await typeRobust(driver, config.selectors.login.password, scenario.Password);
            await driver.findElement(By.css(config.selectors.login.submitBtn)).click();
            await sleep(1500);
            console.log(`${scenario.TestCaseID} Pass: Bị chặn đúng như kỳ vọng.`);
        }

        if (adminLogin) {
            console.log(`Đang chạy ${adminLogin.TestCaseID}: ${adminLogin.Description}...`);
            await driver.get(config.app.baseUrl + '/Account/Login');
            await typeRobust(driver, config.selectors.login.email, adminLogin.Email);
            await typeRobust(driver, config.selectors.login.password, adminLogin.Password);
            await driver.findElement(By.css(config.selectors.login.submitBtn)).click();
            await sleep(2000);
            console.log(`${adminLogin.TestCaseID} Pass: Admin đăng nhập thành công.`);
        }

        // Gọi đúng selector tên createUser từ file config.js
        const selCreate = config.selectors.createUser;

        for (const scenario of createFailures) {
            console.log(`Đang chạy ${scenario.TestCaseID}: ${scenario.Description}...`);
            await driver.get(config.app.baseUrl + '/Admin/Users/Create');
            await sleep(1000);

            await typeRobust(driver, selCreate.fullName, scenario.FullName);
            await typeRobust(driver, selCreate.email, scenario.Email);
            await typeRobust(driver, selCreate.password, scenario.Password);
            await typeRobust(driver, selCreate.phone, scenario.Phone);
            await typeRobust(driver, selCreate.qualifications, scenario.Qualifications);

            let dobFormatted = '';
            if (scenario.DOB instanceof Date) {
                const year = scenario.DOB.getFullYear();
                const month = String(scenario.DOB.getMonth() + 1).padStart(2, '0');
                const day = String(scenario.DOB.getDate()).padStart(2, '0');
                dobFormatted = `${year}-${month}-${day}`;
            } else {
                dobFormatted = scenario.DOB ? scenario.DOB.toString().trim() : '';
            }
            await setDatePicker(driver, selCreate.dob, dobFormatted);

            await selectDropdownByClick(driver, selCreate.roleId, 'TicketAgent');

            if (scenario.Email) {
                await driver.findElement(By.css(selCreate.email)).clear();
                await driver.findElement(By.css(selCreate.email)).sendKeys(scenario.Email.toString().trim());
            }
            if (scenario.Password) {
                await driver.findElement(By.css(selCreate.password)).clear();
                await driver.findElement(By.css(selCreate.password)).sendKeys(scenario.Password.toString().trim());
            }

            await driver.findElement(By.css(selCreate.submitBtn)).click();
            await sleep(1500);

            const currentUrl = await driver.getCurrentUrl();
            if (currentUrl.includes('/Admin/Users/Create')) {
                console.log(`${scenario.TestCaseID} Pass: Đã chặn tạo tài khoản thành công.`);
            } else {
                console.log(`${scenario.TestCaseID} Fail: Lọt lỗi lên hệ thống!`);
            }

            // === TỰ ĐỘNG DỌN DẸP NẾU CASE LỖI BỊ LỌT VÀO DB ===
            if (scenario.Email) {
                await userCollection.deleteMany({ email: { $regex: new RegExp(`^${scenario.Email}$`, 'i') } });
            }
        }

        if (createSuccess) {
            console.log(`Đang chạy ${createSuccess.TestCaseID}: ${createSuccess.Description}...`);
            await driver.get(config.app.baseUrl + '/Admin/Users/Create');
            await sleep(1000);

            await typeRobust(driver, selCreate.fullName, createSuccess.FullName);
            await typeRobust(driver, selCreate.email, empEmail);
            await typeRobust(driver, selCreate.password, empPass);
            await typeRobust(driver, selCreate.phone, createSuccess.Phone);
            await typeRobust(driver, selCreate.qualifications, createSuccess.Qualifications);

            let dobFormatted = '';
            if (createSuccess.DOB instanceof Date) {
                const year = createSuccess.DOB.getFullYear();
                const month = String(createSuccess.DOB.getMonth() + 1).padStart(2, '0');
                const day = String(createSuccess.DOB.getDate()).padStart(2, '0');
                dobFormatted = `${year}-${month}-${day}`;
            } else {
                dobFormatted = createSuccess.DOB ? createSuccess.DOB.toString().trim() : '';
            }
            await setDatePicker(driver, selCreate.dob, dobFormatted);

            await selectDropdownByClick(driver, selCreate.roleId, 'TicketAgent');

            await driver.findElement(By.css(selCreate.email)).clear();
            await driver.findElement(By.css(selCreate.email)).sendKeys(empEmail);
            await driver.findElement(By.css(selCreate.password)).clear();
            await driver.findElement(By.css(selCreate.password)).sendKeys(empPass);

            await driver.findElement(By.css(selCreate.submitBtn)).click();
            await driver.wait(until.urlContains('/Admin/Users'), 10000);

            let newEmpRecord = null;
            const dbQuery = { email: { $regex: new RegExp(`^${empEmail}$`, 'i') } };

            for (let i = 0; i < 5; i++) {
                newEmpRecord = await userCollection.findOne(dbQuery);
                if (newEmpRecord) break;
                await sleep(1000);
            }
            if (newEmpRecord) console.log(`${createSuccess.TestCaseID} Pass: Tìm thấy Employee trong MongoDB!`);

            console.log("Đang chạy TC_34: Thử Đăng nhập bằng tài khoản mới...");
            await driver.get(config.app.baseUrl + '/Account/Logout');
            await sleep(1000);

            await driver.get(config.app.baseUrl + '/Account/Login');
            await typeRobust(driver, config.selectors.login.email, empEmail);
            await typeRobust(driver, config.selectors.login.password, empPass);
            await driver.findElement(By.css(config.selectors.login.submitBtn)).click();
            await sleep(2000);
            console.log("TC_34 Pass: Employee đã đăng nhập thành công!");

            // === XÓA LUÔN ACCOUNT TEST THÀNH CÔNG ===
            if (newEmpRecord) await userCollection.deleteOne(dbQuery);
        }

        console.log("=== HOÀN TẤT TOÀN BỘ KỊCH BẢN KIỂM THỬ ===");

    } catch (error) {
        console.error("Lỗi trong quá trình chạy:", error);
    } finally {
        await driver.quit();
        await client.close();
    }
}

runAuthAutomation();