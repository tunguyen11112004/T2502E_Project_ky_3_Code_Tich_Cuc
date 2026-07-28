module.exports = {
    db: {
        uri: "mongodb://127.0.0.1:27017",
        name: "BusTicketDb",
        collection: "users"
    },
    app: {
        baseUrl: "http://localhost:5280"
    },
    credentials: {
        admin: {
            email: 'admin@src.com',
            pass: 'Admin@123'
        }
    },
    selectors: {
        login: {
            email: '#Email',
            password: '#Password',
            submitBtn: 'button[type="submit"]'
        },
        createUser: {
            fullName: '#FullName',
            email: '#Email',
            password: '#Password',
            phone: '#PhoneNumber',
            qualifications: '#Qualifications',
            dob: '#Dob',
            roleId: '#RoleId',
            submitBtn: 'button.bg-blue-600[type="submit"]'
        }
    }
};