# 🚗 AI-Based Garage Management System

<div align="center">

**A modern, intelligent garage management platform with AI-powered features**

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)
[![C#](https://img.shields.io/badge/C%23-239120?style=for-the-badge&logo=c-sharp&logoColor=white)](https://docs.microsoft.com/en-us/dotnet/csharp/)
[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-512BD4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/apps/aspnet)
[![License](https://img.shields.io/badge/License-MIT-yellow.svg?style=for-the-badge)](LICENSE)

[Demo](#-demo) • [Features](#-features) • [Installation](#-installation) • [Usage](#-usage) • [API](#-api-documentation) • [Contributing](#-contributing)

</div>

---

## 📋 Overview

The **AI-Based Garage Management System** is a comprehensive platform designed to streamline garage operations through intelligent automation and role-based access control. Built with modern web technologies, it provides seamless interactions between administrators, customers, and mechanics while leveraging AI for enhanced decision-making and customer support.

### 🎯 Key Benefits

- **🤖 AI-Powered Assistance** - Intelligent chatbot for customer queries and support
- **👥 Role-Based Access** - Secure, permission-based functionality for different user types
- **📅 Smart Scheduling** - Automated appointment booking with conflict detection
- **📊 Real-Time Analytics** - Live dashboard with performance metrics
- **🔧 Service Tracking** - Complete vehicle service history and maintenance logs

---

## ✨ Features

### 🛡️ **Role-Based Authentication System**
- **Admin Panel** - Complete system oversight with AI insights
- **Customer Portal** - Self-service booking and service tracking
- **Mechanic Dashboard** - Task management and repair documentation

### 🤖 **AI Integration**
- **Intelligent Chatbot** - 24/7 customer support with natural language processing
- **Predictive Maintenance** - AI-driven service recommendations
- **Smart Scheduling** - Optimal appointment slot suggestions

### 📱 **Modern User Interface**
- **Responsive Design** - Works seamlessly across all devices
- **Interactive Calendar** - Visual appointment management
- **Real-Time Updates** - Live status notifications

### 🔧 **Comprehensive Management Tools**
- **Service History Tracking** - Complete vehicle maintenance records
- **Inventory Management** - Parts and supplies monitoring
- **Customer Relationship Management** - Enhanced customer interaction tracking
- **Reporting & Analytics** - Detailed business insights

---

## 🚀 Demo

### Admin Dashboard with AI Features
*Comprehensive overview with AI-powered insights and management tools*

<div align="center">
  <img src="https://i.imgur.com/ieLPtcd.jpeg" width="80%" alt="Admin Dashboard with AI Integration" style="border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1);"/>
</div>

### Secure Authentication System
*Role-based login with pre-configured credentials for testing*

<div align="center">
  <img src="https://i.imgur.com/NofjI00.jpeg" width="70%" alt="Secure Login Interface" style="border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1);"/>
</div>

### Smart Appointment Calendar
*Intelligent scheduling system with conflict detection and optimization*

<div align="center">
  <img src="https://i.imgur.com/zzsivYu.jpeg" width="70%" alt="Smart Calendar System" style="border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1);"/>
</div>

### Customer Self-Service Portal
*Empowering customers with booking, tracking, and communication tools*

<div align="center">
  <img src="https://i.imgur.com/co930U0.jpeg" width="70%" alt="Customer Dashboard" style="border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1);"/>
</div>

### Mechanic Workflow Management
*Streamlined task management and service documentation*

<div align="center">
  <img src="https://i.imgur.com/OeGksVT.jpeg" width="70%" alt="Mechanic Dashboard" style="border-radius: 8px; box-shadow: 0 4px 8px rgba(0,0,0,0.1);"/>
</div>

---

## 🛠️ Technology Stack

### Backend
- **Framework:** ASP.NET Core 8.0
- **Language:** C# 12
- **Database:** Entity Framework Core with SQL Server
- **Authentication:** ASP.NET Core Identity
- **API:** RESTful Web API with OpenAPI/Swagger

### Frontend
- **Framework:** ASP.NET Core MVC with Razor Pages
- **Styling:** Modern CSS3 with responsive design
- **JavaScript:** Vanilla JS with modern ES6+ features
- **Icons:** Font Awesome / Bootstrap Icons

### AI & ML
- **Chatbot:** Natural Language Processing integration
- **Analytics:** Machine learning for predictive insights

### Development Tools
- **IDE:** Microsoft Visual Studio 2022
- **Version Control:** Git with GitHub
- **Testing:** xUnit for unit testing
- **Documentation:** XML documentation comments

---

## 📦 Installation

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download)
- [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) or [SQL Server Express](https://www.microsoft.com/en-us/sql-server/sql-server-editions-express)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) (recommended) or [VS Code](https://code.visualstudio.com/)

### Quick Start

1. **Clone the repository**
   ```bash
   git clone https://github.com/EliezerKibet/AI_based_garage.git
   cd AI_based_garage
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Configure database connection**
   ```bash
   # Update appsettings.json with your SQL Server connection string
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=GarageManagementDB;Trusted_Connection=true;MultipleActiveResultSets=true"
     }
   }
   ```

4. **Run database migrations**
   ```bash
   dotnet ef database update
   ```

5. **Start the application**
   ```bash
   dotnet run
   ```

6. **Access the application**
   - **HTTPS:** https://localhost:7000
   - **HTTP:** http://localhost:5000

### Development Setup

For development with live reload:
```bash
dotnet watch run
```

---

## 🔑 Usage

### Default Login Credentials

| Role | Username | Password |
|------|----------|----------|
| Admin | `admin@garage.com` | `Admin123!` |
| Customer | `customer@garage.com` | `Customer123!` |
| Mechanic | `mechanic@garage.com` | `Mechanic123!` |

### Getting Started

1. **Admin Users**: Access the admin dashboard to configure system settings, manage users, and view analytics
2. **Customers**: Book appointments, track service history, and communicate with support
3. **Mechanics**: View assigned tasks, update service status, and document repairs

---

## 🔗 API Documentation

The system provides a comprehensive RESTful API for integration with external systems.

### Base URL
```
https://localhost:7000/api/
```

### Key Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| `GET` | `/api/appointments` | Retrieve appointments |
| `POST` | `/api/appointments` | Create new appointment |
| `GET` | `/api/customers/{id}` | Get customer details |
| `POST` | `/api/chatbot/ask` | AI chatbot interaction |

### API Documentation
Access the interactive API documentation at:
- **Swagger UI:** https://localhost:7000/swagger

---

## 🧪 Testing

Run the test suite:
```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

---

## 🚀 Deployment

### Production Deployment

1. **Build for production**
   ```bash
   dotnet publish -c Release -o ./publish
   ```

2. **Configure production settings**
   - Update connection strings
   - Set environment variables
   - Configure HTTPS certificates

3. **Deploy to hosting platform**
   - Azure App Service
   - AWS Elastic Beanstalk
   - Docker containers

---

## 🤝 Contributing

We welcome contributions! Please follow these steps:

1. **Fork the repository**
2. **Create a feature branch**
   ```bash
   git checkout -b feature/amazing-feature
   ```
3. **Commit your changes**
   ```bash
   git commit -m 'Add amazing feature'
   ```
4. **Push to the branch**
   ```bash
   git push origin feature/amazing-feature
   ```
5. **Open a Pull Request**

### Development Guidelines

- Follow C# coding conventions
- Write unit tests for new features
- Update documentation as needed
- Ensure all tests pass before submitting

---

## 📄 License

This project is licensed under the **MIT License** - see the [LICENSE](LICENSE) file for details.

---

## 📞 Support & Contact

- **GitHub Issues:** [Report a bug or request a feature](https://github.com/EliezerKibet/AI_based_garage/issues)
- **Developer:** [Eliezer Kibet](https://github.com/EliezerKibet)
- **Email:** [Contact for support](mailto:elieserkibet@gmail.com)

---

## 🌟 Acknowledgments

- Built with ❤️ using ASP.NET Core
- Icons by [Font Awesome](https://fontawesome.com/)
- AI capabilities powered by modern NLP technologies
- Special thanks to the open-source community

---

<div align="center">

**⭐ Star this repository if you find it helpful!**

[⬆ Back to Top](#-ai-based-garage-management-system)

</div>