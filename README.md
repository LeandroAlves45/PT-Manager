# PT Manager – Personal Training Management Platform 💪

A **production-ready, full-stack SaaS application** for personal trainers to manage clients, training sessions, workouts, nutrition, and billing. Built with **FastAPI** (backend), **React** (frontend), and **PostgreSQL**.

---

## Table of Contents

- [Overview](#overview)
- [Tech Stack](#tech-stack)
- [Project Structure](#project-structure)
- [Features](#features)
- [Getting Started](#getting-started)
  - [Prerequisites](#prerequisites)
  - [Quick Start with Docker](#quick-start-with-docker)
  - [Local Development Setup](#local-development-setup)
- [Backend Setup](#backend-setup)
- [Frontend Setup](#frontend-setup)
- [Environment Variables](#environment-variables)
- [Running the Application](#running-the-application)
- [API Reference](#api-reference)
- [Testing](#testing)
- [Deployment](#deployment)
- [License](#license)

---

## Overview

**PT Manager** is a comprehensive SaaS platform that enables personal trainers to:

- **Manage clients** with detailed profiles and progress tracking
- **Schedule and track training sessions** with automated pack consumption
- **Create personalized workout programs** with exercises and progression schemes
- **Design meal plans** with macro calculations and nutrition tracking
- **Manage supplements** and supplement recommendations
- **Track progress** through assessments and periodic check-ins
- **Handle billing** with Stripe integration and automatic tier management
- **Engage clients** via a dedicated client portal and in-app notifications

Each trainer operates in an **isolated tenant environment**, seeing only their own clients and data.

---

## Tech Stack

### Backend
| Layer | Technology |
|-------|-----------|
| Framework | FastAPI 0.115 |
| ORM | SQLModel 0.0.22 (SQLAlchemy + Pydantic) |
| Database | PostgreSQL 17 (SQLite for local dev) |
| Authentication | JWT + passlib/bcrypt |
| Payments | Stripe API |
| Email | Resend API |
| Image Storage | Cloudinary |
| Background Jobs | APScheduler 3.10 |
| Server | Uvicorn + Gunicorn |
| Testing | pytest + httpx + pytest-cov |
| Python | 3.12 |

### Frontend
| Layer | Technology |
|-------|-----------|
| Framework | React 19 |
| Language | TypeScript 5.9 |
| Build Tool | Vite 7.3 |
| Styling | Tailwind CSS 4 + PostCSS |
| UI Components | Chakra UI + Radix UI |
| Form Handling | React Hook Form |
| Routing | React Router 7 |
| HTTP Client | Axios |
| Charts | Recharts 3.7 |
| Testing | Vitest + Testing Library |
| Linting | ESLint 9 |
| Formatting | Prettier 3.8 |

### Deployment
| Service | Purpose |
|---------|---------|
| Docker | Containerization |
| Render.com | Backend hosting |
| Vercel | Frontend hosting (optional) |

---

## Project Structure

```text
Projeto_pt_manager/
├── backend/                           # FastAPI backend
│   ├── app/
│   │   ├── main.py                   # App factory & lifecycle hooks
│   │   ├── scheduler.py              # APScheduler setup
│   │   ├── api/
│   │   │   ├── deps.py               # FastAPI dependencies (auth, DB)
│   │   │   └── v1/                   # All API routes
│   │   ├── core/
│   │   │   ├── config.py             # Settings from .env
│   │   │   ├── security.py           # JWT, password hashing, RBAC
│   │   │   └── logging.py            # Logging config
│   │   ├── crud/                     # Data access layer
│   │   ├── db/
│   │   │   ├── models/               # SQLModel ORM models
│   │   │   ├── migrations/           # SQL migration files
│   │   │   └── seeds/                # Seed data scripts
│   │   ├── schemas/                  # Pydantic request/response schemas
│   │   ├── services/                 # Business logic
│   │   └── utils/                    # Helper utilities
│   ├── tests/                        # Test suite
│   ├── .env.example                  # Environment template
│   ├── Dockerfile                    # Container image
│   ├── docker-compose.yml            # Local dev environment
│   ├── requirements.txt               # Python dependencies
│   ├── pytest.ini                    # Test config
│   └── render.yaml                   # Render.com deployment
│
├── frontend/                          # React frontend
│   ├── src/
│   │   ├── components/               # Reusable React components
│   │   ├── pages/                    # Page components
│   │   ├── hooks/                    # Custom React hooks
│   │   ├── services/                 # API client & business logic
│   │   ├── styles/                   # Global styles & Tailwind config
│   │   ├── utils/                    # Helper functions
│   │   └── App.tsx                   # Main app component
│   ├── tests/                        # Test suite
│   ├── .env                          # Frontend env (client-side)
│   ├── .env.example                  # Environment template
│   ├── vite.config.ts                # Vite bundler config
│   ├── tsconfig.json                 # TypeScript config
│   ├── tailwind.config.ts            # Tailwind CSS config
│   ├── package.json                  # NPM dependencies
│   ├── eslint.config.js              # Linting rules
│   ├── vercel.json                   # Vercel deployment (optional)
│   └── vitest.config.js              # Vitest test runner config
│
├── .gitignore                        # Git ignore rules
├── README.md                         # This file
├── LICENSE                           # Project license
└── docker-compose.yml                # Multi-service orchestration (optional)
```

---

## Features

### Core Features
- ✅ **Multi-tenancy** – Trainers are fully isolated; each sees only their own data
- ✅ **Role-Based Access Control** – Three roles: `superuser`, `trainer`, `client`
- ✅ **JWT Authentication** with logout support via database token tracking
- ✅ **Subscription Billing** via Stripe with automatic tier management
- ✅ **Training Session Management** – Schedule, complete, cancel, or mark sessions missed
- ✅ **Session Pack System** – Clients purchase packs; sessions auto-consume on completion
- ✅ **Workout Program Builder** – Multi-day plans with exercises and set/rep schemes
- ✅ **Nutrition Module** – Food database, meal plans, and macro calculator
- ✅ **Supplement Tracking** – Catalog and per-client supplement assignments
- ✅ **Progress Tracking** – Initial assessments & periodic check-ins
- ✅ **In-App Notifications** – Session reminders via background jobs
- ✅ **Email Notifications** – Transactional email via Resend
- ✅ **Image Uploads** – Cloudinary integration for logos and photos
- ✅ **Admin Dashboard** – Platform metrics, trainer management, billing exemptions
- ✅ **Client Portal** – Dedicated dashboard for clients to view plans and log progress

### Technical Features
- ✅ **Idempotent SQL Migrations** – Safe to run on every deploy
- ✅ **Soft Deletes** – Archive data instead of permanently deleting
- ✅ **Docker Compose** for local development
- ✅ **Comprehensive API Documentation** – Swagger UI at `/docs`
- ✅ **Automated Testing** – Unit, integration, and E2E tests
- ✅ **Type Safety** – Full TypeScript frontend & Pydantic schemas backend

---

## Getting Started

### Prerequisites

**For Backend:**
- Python 3.12+
- PostgreSQL 17 (or use Docker Compose)

**For Frontend:**
- Node.js 18+
- npm or yarn

**For Both:**
- Docker & Docker Compose (recommended)
- Git

**Third-party Services (for full functionality):**
- Stripe account (billing)
- Resend account (email)
- Cloudinary account (image uploads)

---

### Quick Start with Docker

```bash
# Clone the repository
git clone https://github.com/LeandroAlves45/Project_pt_manager.git
cd Projeto_pt_manager

# Start all services (PostgreSQL + Backend API + Frontend)
docker-compose up --build

# Backend will be available at http://localhost:8000
# Frontend will be available at http://localhost:5173
# API docs at http://localhost:8000/docs
```

---

### Local Development Setup

#### Backend Setup

```bash
# Navigate to backend directory
cd backend

# Create virtual environment
python -m venv venv

# Activate virtual environment
# On Windows:
venv\Scripts\activate
# On macOS/Linux:
source venv/bin/activate

# Install dependencies
pip install -r requirements.txt

# Copy environment file and configure
cp .env.example .env
# Edit .env with your values (database, Stripe, Resend, etc.)

# Run the application
uvicorn app.main:app --reload --port 8000
```

**Note:** Database tables are created automatically on startup. Migrations run idempotently, so no manual migration step is needed.

#### Frontend Setup

```bash
# Navigate to frontend directory
cd frontend

# Install dependencies
npm install

# Copy environment file
cp .env.example .env
# Edit .env with backend API URL (e.g., http://localhost:8000)

# Start development server
npm run dev

# The frontend will be available at http://localhost:5173
```

---

## Backend Setup

### Environment Variables

Copy `.env.example` to `.env` and configure:

```bash
cd backend
cp .env.example .env
```

| Variable | Description | Required |
|----------|-------------|----------|
| `DATABASE_URL` | PostgreSQL connection string | Yes |
| `SECRET_KEY` | JWT signing key | Yes |
| `API_KEY` | API middleware key | Yes |
| `ACCESS_TOKEN_EXPIRE_MINUTES` | JWT lifetime in minutes | No (default: 60) |
| `TRIAL_DAYS` | Free trial period | No (default: 15) |
| `CORS_ORIGINS` | Comma-separated allowed origins | Yes |
| `STRIPE_SECRET_KEY` | Stripe API secret key | Yes |
| `STRIPE_WEBHOOK_SECRET` | Stripe webhook signing secret | Yes |
| `STRIPE_PRICE_STARTER` | Stripe Starter tier price ID | Yes |
| `STRIPE_PRICE_PRO` | Stripe Pro tier price ID | Yes |
| `RESEND_API_KEY` | Resend API key for email | Yes |
| `EMAIL_FROM` | Sender email address | Yes |
| `CLOUDINARY_CLOUD_NAME` | Cloudinary cloud name | No |
| `CLOUDINARY_API_KEY` | Cloudinary API key | No |
| `CLOUDINARY_API_SECRET` | Cloudinary API secret | No |
| `SUPERUSER_EMAIL` | Seed superuser email | No |
| `SUPERUSER_PASSWORD` | Seed superuser password | No |
| `SEED_DEMO_DATA` | Enable demo data seeding | No (default: false) |
| `TIMEZONE` | Scheduler timezone | No (default: UTC) |

---

## Frontend Setup

### Environment Variables

Copy `.env.example` to `.env` and configure:

```bash
cd frontend
cp .env.example .env
```

| Variable | Description | Example |
|----------|-------------|---------|
| `VITE_API_URL` | Backend API base URL | `http://localhost:8000` |
| `VITE_APP_NAME` | Application name | `PT Manager` |

---

## Running the Application

### Development Mode

**Start both services in parallel:**

```bash
# Terminal 1: Backend
cd backend
source venv/bin/activate  # or venv\Scripts\activate on Windows
uvicorn app.main:app --reload --port 8000

# Terminal 2: Frontend
cd frontend
npm run dev
```

Access:
- **Frontend:** http://localhost:5173
- **Backend API:** http://localhost:8000
- **API Docs:** http://localhost:8000/docs

### Using Docker Compose

```bash
docker-compose up --build
```

### Production Build

**Frontend:**
```bash
cd frontend
npm run build
npm run preview
```

**Backend with Gunicorn:**
```bash
cd backend
gunicorn -w 4 -k uvicorn.workers.UvicornWorker app.main:app --bind 0.0.0.0:8000
```

---

## API Reference

Base URL: `/api/v1`

### Authentication

| Method | Path | Description |
|--------|------|-------------|
| POST | `/auth/login` | Login & receive JWT |
| POST | `/auth/logout` | Invalidate token |
| POST | `/signup/trainer` | Register new trainer |
| GET | `/auth/users/me` | Get current user profile |

### Core Resources

| Resource | Methods | Description |
|----------|---------|-------------|
| `/clients` | GET, POST, PATCH, DELETE | Client management |
| `/sessions` | GET, POST, PUT | Training session scheduling |
| `/packs` | POST, GET | Session pack purchases |
| `/training-plans` | GET, POST, PUT, DELETE | Workout program templates |
| `/exercises` | GET, POST, PUT, DELETE | Exercise library |
| `/nutrition` | GET, POST, PATCH | Meal plans & macro calculations |
| `/supplements` | GET, POST, PATCH | Supplement catalog |
| `/assessments` | POST, GET | Client assessments |
| `/checkins` | POST, GET | Progress check-ins |
| `/billing` | GET, POST | Subscription & Stripe integration |

**Full API documentation available at http://localhost:8000/docs**

---

## Testing

### Backend Tests

```bash
cd backend

# Run all tests
pytest

# Run with coverage report
pytest --cov=app --cov-report=term-missing

# Run specific test file
pytest tests/unit/test_macro_calculator.py -v
```

### Frontend Tests

```bash
cd frontend

# Run tests
npm run test

# Run with coverage
npm run test:coverage
```

---

## Deployment

### Backend (Render.com)

1. Connect your GitHub repo to Render
2. Create a new Web Service
3. Set environment variables in Render dashboard
4. Render detects `render.yaml` and deploys automatically

The API will be available at your Render URL (e.g., `https://pt-manager-api.onrender.com`).

### Frontend (Vercel / Netlify)

**Vercel:**
```bash
npm i -g vercel
vercel
```

**Netlify:**
```bash
npm i -g netlify-cli
netlify deploy
```

---

## Git Workflow

```bash
# Create feature branch
git checkout -b feature/my-feature

# Make changes
git add .
git commit -m "feat: add my feature"

# Push and create pull request
git push origin feature/my-feature
```

---

## Troubleshooting

### Backend Issues

- **Database connection error:** Verify `DATABASE_URL` in `.env`
- **Port 8000 already in use:** Change port or kill existing process
- **Module not found:** Ensure virtual environment is activated and dependencies are installed

### Frontend Issues

- **API connection error:** Verify `VITE_API_URL` points to running backend
- **Port 5173 already in use:** Vite will automatically use next available port
- **Build errors:** Run `npm ci` to install exact dependency versions

### Docker Issues

```bash
# Rebuild images
docker-compose down
docker-compose up --build

# View logs
docker-compose logs -f backend
docker-compose logs -f frontend

# Remove all containers & volumes
docker-compose down -v
```

---

## Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## Support

For issues, questions, or suggestions, please open an issue on GitHub.

---

## License

This project is proprietary software. All rights reserved. See [LICENSE](./LICENSE) for details.

---

**Built with ❤️ by Leandro Alves**

[GitHub](https://github.com/LeandroAlves45) | [Email](mailto:ptleoalves@gmail.com)
