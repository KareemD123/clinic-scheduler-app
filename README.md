# Clinic Scheduling & Billing System | AI Assisted Development

A full-stack application for managing patient appointments and billing in a small clinic, built with a .NET 8 backend and an Angular frontend.

## Architecture Overview

The project follows a clean, service-oriented architecture to ensure a clear separation of concerns, testability, and maintainability.

- **Frontend**: An Angular single-page application (SPA) provides a dynamic user interface.
- **Backend**: A .NET 8 API serves data and handles business logic.
- **Data Persistence**: A simple JSON file acts as the database, managed by a thread-safe repository pattern.
- **Containerization**: Docker is used to containerize both the frontend and backend for consistent, isolated deployments.

## Key Features

- **Patient Management**: Create and manage patient records.
- **Appointment Scheduling**: Schedule appointments between patients and doctors.
- **Transactional Billing**: Atomically schedule an appointment and generate an invoice. If invoice generation fails, the entire transaction is rolled back.
- **Payment Processing**: Process payments for invoices and generate a receipt.
- **Clean API Design**: A RESTful API with a consistent response format and error handling.

## Technology Stack

- **Backend**: .NET 8, ASP.NET Core, xUnit, Moq, FluentAssertions
- **Frontend**: Angular 12, TypeScript, RxJS, SCSS
- **Database**: JSON file
- **Containerization**: Docker, Docker Compose, Nginx

## Getting Started

### Prerequisites

- .NET 8.0 SDK
- Node.js 18+ and npm
- Docker Desktop

### 1. Run with Docker (Recommended)

This is the simplest method to get the application running.

```bash
# From the project root directory
docker compose up --build
```

- **Frontend UI**: [http://localhost:4200](http://localhost:4200)
- **Backend API**: [http://localhost:5000](http://localhost:5000)
- **Swagger UI**: [http://localhost:5000/swagger](http://localhost:5000/swagger)

### 2. Manual Local Setup

Run the backend and frontend in separate terminals.

**Terminal 1: Run Backend API**

```bash
cd backend
dotnet run --project ClinicScheduling.API/ClinicScheduling.API.csproj
```

> The backend will be available at `http://localhost:5187` (or another port if 5187 is busy).

**Terminal 2: Run Frontend UI**

```bash
cd frontend/clinic-scheduling-ui

# Required for Node.js v17+ due to OpenSSL changes
export NODE_OPTIONS=--openssl-legacy-provider

npm install
ng serve
```

> The frontend will be available at `http://localhost:4200`.

### 3. Run Tests

**Backend Unit Tests**

```bash
cd backend
dotnet test
```

**Frontend Unit Tests**

```bash
cd frontend/clinic-scheduling-ui
export NODE_OPTIONS=--openssl-legacy-provider
ng test
```

