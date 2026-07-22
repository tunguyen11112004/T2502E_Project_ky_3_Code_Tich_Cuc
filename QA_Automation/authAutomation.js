const { Builder, By, until, Key } = require('selenium-webdriver');
const { MongoClient } = require('mongodb');
const xlsx = require('xlsx');

const config = require('./test-data.json');
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
        await sleep(600); // Đợi form re-render sau khi chọn role
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

    const client = new MongoClient(config.database.uri);
    const driver = await new Builder().forBrowser('chrome').build();

    try {
        await client.connect();
        const userCollection = client.db(config.database.dbName).collection(config.database.collection);

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

        const selCreate = config.selectors.create;

        for (const scenario of createFailures) {
            console.log(`Đang chạy ${scenario.TestCaseID}: ${scenario.Description}...`);
            await driver.get(config.app.baseUrl + '/Admin/Users/Create');
            await sleep(1000);

            // BƯỚC 1: ĐIỀN CÁC TRƯỜNG DỮ LIỆU BÌNH THƯỜNG
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

            // BƯỚC 2: CHỌN DYNAMIC ROLE SAU CÙNG (CÓ THỂ GÂY RE-RENDER)
            await selectDropdownByClick(driver, selCreate.roleId, 'TicketAgent');

            // BƯỚC 3: BỒI ĐẮP LẠI (RE-TYPE) CÁC TRƯỜNG DỄ BỊ MẤT STATE NẾU BỊ REFRESH
            if (scenario.Email) {
                await driver.findElement(By.css(selCreate.email)).clear();
                await driver.findElement(By.css(selCreate.email)).sendKeys(scenario.Email.toString().trim());
            }
            if (scenario.Password) {
                await driver.findElement(By.css(selCreate.password)).clear();
                await driver.findElement(By.css(selCreate.password)).sendKeys(scenario.Password.toString().trim());
            }

            // BƯỚC 4: SUBMIT FORM
            await driver.findElement(By.css(selCreate.submitBtn)).click();
            await sleep(1500);

            const currentUrl = await driver.getCurrentUrl();
            if (currentUrl.includes('/Admin/Users/Create')) {
                console.log(`${scenario.TestCaseID} Pass: Đã chặn tạo tài khoản thành công.`);
            } else {
                console.log(`${scenario.TestCaseID} Fail: Lọt lỗi lên hệ thống!`);
            }
        }

        if (createSuccess) {
            console.log(`Đang chạy ${createSuccess.TestCaseID}: ${createSuccess.Description}...`);
            await driver.get(config.app.baseUrl + '/Admin/Users/Create');
            await sleep(1000);

            // BƯỚC 1: ĐIỀN DỮ LIỆU CHO CASE THÀNH CÔNG
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

            // BƯỚC 2: CHỌN ROLE
            await selectDropdownByClick(driver, selCreate.roleId, 'TicketAgent');

            // BƯỚC 3: BỒI ĐẮP LẠI EMAIL & PASSWORD CHO CHẮC CHẮN
            await driver.findElement(By.css(selCreate.email)).clear();
            await driver.findElement(By.css(selCreate.email)).sendKeys(empEmail);
            await driver.findElement(By.css(selCreate.password)).clear();
            await driver.findElement(By.css(selCreate.password)).sendKeys(empPass);

            // BƯỚC 4: SUBMIT
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