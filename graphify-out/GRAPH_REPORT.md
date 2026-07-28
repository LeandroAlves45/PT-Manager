# Graph Report - .  (2026-07-28)

## Corpus Check
- cluster-only mode — file stats not available

## Summary
- 2060 nodes · 3732 edges · 211 communities (167 shown, 44 thin omitted)
- Extraction: 98% EXTRACTED · 2% INFERRED · 0% AMBIGUOUS · INFERRED: 72 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Graph Freshness
- Built from commit: `9128929f`
- Run `git rev-parse HEAD` and compare to check if the graph is stale.
- Run `graphify update .` after code changes (no API cost).

## Community Hubs (Navigation)
- Sessions.jsx
- useAuth
- PTManager.sln
- card.tsx
- MealPlan
- DurableJob
- Domain.Exceptions
- net10.0
- SupplementPage.jsx
- Checkin
- TrainerSubscription
- compilerOptions
- adminApi.js
- cn
- Domain.Entities.Training
- AssessmentPage.jsx
- compilerOptions
- MealsPlanPage.jsx
- trainingPlan.js
- Exercises.jsx
- axiosConfig.js
- User
- Clients.jsx
- dependencies
- Session
- net10.0
- Microsoft.Testing.Platform
- net10.0
- Microsoft.Testing.Platform
- net10.0
- MyProfile.jsx
- Notification
- net10.0
- net10.0
- compilerOptions
- Client
- http
- Microsoft.AspNetCore.OpenApi
- Microsoft.AspNetCore.OpenApi
- App.jsx
- Supplement
- xunit.v3.extensibility.core
- xunit.v3.extensibility.core
- xunit.v3.extensibility.core
- xunit.v3.extensibility.core
- xunit.v3.extensibility.core
- chart.tsx
- ClientSessionPack
- dependencies
- dependencies
- dependencies
- dependencies
- dependencies
- devDependencies
- carousel.tsx
- PackType
- Food
- ClientExerciseSetLog
- TrainingPlan
- Microsoft.Testing.Platform
- Microsoft.Testing.Platform
- Microsoft.Testing.Platform
- supplementApi.js
- Domain.Entities.Billing
- RefreshToken
- net10.0
- xunit.v3.mtp-v1
- xunit.v3.mtp-v1
- xunit.v3.mtp-v1
- xunit.v3.mtp-v1
- xunit.v3.mtp-v1
- PlanDaysList.jsx
- Application
- Microsoft.NET.Test.Sdk
- Application
- Microsoft.NET.Test.Sdk
- Domain
- Microsoft.NET.Test.Sdk
- Microsoft.NET.Test.Sdk
- Microsoft.NET.Test.Sdk
- scripts
- exercisesApi.js
- command.tsx
- context-menu.tsx
- InviteToken
- Exercise
- SubscriptionStatus
- xunit.v3.runner.inproc.console
- xunit.v3.runner.inproc.console
- xunit.v3.runner.inproc.console
- xunit.v3.runner.inproc.console
- xunit.v3.runner.inproc.console
- ITenantContext.cs
- TrainerSettings
- xunit.v3
- xunit.v3
- xunit.v3
- xunit.v3
- xunit.v3
- EmailAddressTests
- breadcrumb.tsx
- drawer.tsx
- xunit.v3.common
- Microsoft.TestPlatform.TestHost
- xunit.v3.common
- Microsoft.TestPlatform.TestHost
- Microsoft.Testing.Extensions.Telemetry
- xunit.v3.common
- Microsoft.TestPlatform.TestHost
- Microsoft.Testing.Extensions.Telemetry
- xunit.v3.common
- Microsoft.TestPlatform.TestHost
- Microsoft.Testing.Extensions.Telemetry
- xunit.v3.common
- Microsoft.TestPlatform.TestHost
- inviteApi.js
- badge.tsx
- packages.lock.json
- OutboxMessage
- Domain.Entities.Nutrition
- coverlet.collector
- Microsoft.OpenApi
- coverlet.collector
- Microsoft.OpenApi
- xunit.runner.visualstudio
- application
- coverlet.collector
- xunit.runner.visualstudio
- coverlet.collector
- xunit.runner.visualstudio
- MacroSummaryTests
- package.json
- alert.tsx
- packages.lock.json
- Microsoft.ApplicationInsights
- Microsoft.CodeCoverage
- Microsoft.Testing.Platform
- Microsoft.TestPlatform.ObjectModel
- xunit.v3.assert
- xunit.v3.assert
- Microsoft.ApplicationInsights
- xunit.analyzers
- Microsoft.CodeCoverage
- Microsoft.Testing.Platform
- Microsoft.TestPlatform.ObjectModel
- xunit.v3.assert
- Microsoft.ApplicationInsights
- Microsoft.Testing.Platform
- Microsoft.TestPlatform.ObjectModel
- Microsoft.Win32.Registry
- xunit.analyzers
- xunit.analyzers
- Microsoft.Bcl.AsyncInterfaces
- Microsoft.Testing.Platform
- Microsoft.TestPlatform.ObjectModel
- Microsoft.Win32.Registry
- xunit.analyzers
- Microsoft.CodeCoverage
- Microsoft.Testing.Platform
- Microsoft.TestPlatform.ObjectModel
- xunit.analyzers
- xunit.v3.assert
- DomainException.cs
- @eslint/js
- clsx
- cmdk
- embla-carousel-react
- eslint-config-prettier
- class-variance-authority
- eslint-plugin-react-hooks
- eslint-plugin-react-refresh
- @radix-ui/react-accordion
- @radix-ui/react-alert-dialog
- @radix-ui/react-aspect-ratio
- @radix-ui/react-avatar
- @radix-ui/react-context-menu
- @radix-ui/react-dropdown-menu
- @radix-ui/react-label
- @radix-ui/react-progress
- @radix-ui/react-select
- @radix-ui/react-separator
- @radix-ui/react-slot
- @radix-ui/react-switch
- @radix-ui/react-tabs
- react-hook-form
- react-router-dom
- react-toastify
- tailwind-merge
- globals
- jsdom
- msw
- tailwindcss
- @tailwindcss/vite
- @testing-library/react
- @testing-library/user-event
- @types/react
- @types/react-dom
- typescript
- vite
- @vitejs/plugin-react
- vitest
- @vitest/coverage-v8
- vercel.json
- vitest.config.js

## God Nodes (most connected - your core abstractions)
1. `cn()` - 61 edges
2. `Domain.Exceptions` - 56 edges
3. `Button` - 39 edges
4. `net10.0` - 29 edges
5. `net10.0` - 29 edges
6. `Badge()` - 29 edges
7. `Input` - 28 edges
8. `net10.0` - 26 edges
9. `Card` - 26 edges
10. `CardContent` - 26 edges

## Surprising Connections (you probably didn't know these)
- `AlertsPanel()` --indirect_call--> `Calendar()`  [INFERRED]
  frontend/src/components/dashboard/AlertsPanel.jsx → frontend/src/components/ui/calendar.tsx
- `BreadcrumbSeparator()` --calls--> `cn()`  [EXTRACTED]
  frontend/src/components/ui/breadcrumb.tsx → frontend/src/lib/utils.ts
- `BreadcrumbEllipsis()` --calls--> `cn()`  [EXTRACTED]
  frontend/src/components/ui/breadcrumb.tsx → frontend/src/lib/utils.ts
- `CommandShortcut()` --calls--> `cn()`  [EXTRACTED]
  frontend/src/components/ui/command.tsx → frontend/src/lib/utils.ts
- `ContextMenuShortcut()` --calls--> `cn()`  [EXTRACTED]
  frontend/src/components/ui/context-menu.tsx → frontend/src/lib/utils.ts

## Import Cycles
- None detected.

## Communities (211 total, 44 thin omitted)

### Community 0 - "Sessions.jsx"
Cohesion: 0.09
Nodes (47): getClient(), cancelSession(), completeSession(), createSession(), getSessions(), markSessionMissed(), ClientTable(), ClientsAtRisk() (+39 more)

### Community 1 - "useAuth"
Cohesion: 0.06
Nodes (41): getPortalBranding(), App(), getDashboardForRole(), ProtectedRoute(), mockUseAuth, bottomNavItems, isItemActive(), NavContent() (+33 more)

### Community 2 - "PTManager.sln"
Cohesion: 0.06
Nodes (31): Microsoft.NET.Sdk, Microsoft.NET.Sdk, Microsoft.NET.Sdk, coverlet.collector, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, xunit.v3, Microsoft.NET.Sdk (+23 more)

### Community 3 - "card.tsx"
Cohesion: 0.13
Nodes (28): changeMyPassword(), getMyCheckIns(), getMyMealPlans(), getMyTrainingPlan(), respondToCheckIn(), upsertExerciseSetLogs(), AlertsPanel(), Card (+20 more)

### Community 4 - "MealPlan"
Cohesion: 0.09
Nodes (22): DateOnly, DateTime, Guid, IReadOnlyCollection, List, MealPlan, DateTime, Guid (+14 more)

### Community 5 - "DurableJob"
Cohesion: 0.14
Nodes (13): DateTime, Guid, DurableJob, JobStatus, Fact, DurableJobTests, Fact, InlineData (+5 more)

### Community 6 - "Domain.Exceptions"
Cohesion: 0.13
Nodes (9): Domain.UnitTests.Entities.Notifications, Domain.ValueObjects, Domain.Entities.Notifications, Domain.Entities.Identity, Unit.Domain.UnitTests.Entities.Identity, Domain.Entities.Jobs, Unit.Domain.UnitTests.ValueObjects, Domain.Exceptions (+1 more)

### Community 7 - "net10.0"
Cohesion: 0.07
Nodes (28): dependencies, type, dependencies, Application, Domain, Microsoft.OpenApi, net10.0, type (+20 more)

### Community 8 - "SupplementPage.jsx"
Cohesion: 0.34
Nodes (12): Button, DialogContent, DialogDescription, DialogHeader(), DialogOverlay, DialogTitle, Input, Label (+4 more)

### Community 9 - "Checkin"
Cohesion: 0.12
Nodes (13): DateOnly, DateTime, Guid, Checkin, DateTime, Guid, InitialAssessment, Fact (+5 more)

### Community 10 - "TrainerSubscription"
Cohesion: 0.15
Nodes (11): DateTime, Guid, TrainerSubscription, SubscriptionTier, Fact, TrainerSubscriptionTests, Fact, MemberData (+3 more)

### Community 11 - "compilerOptions"
Cohesion: 0.08
Nodes (25): compilerOptions, allowSyntheticDefaultImports, baseUrl, esModuleInterop, forceConsistentCasingInFileNames, isolatedModules, jsx, lib (+17 more)

### Community 12 - "adminApi.js"
Cohesion: 0.13
Nodes (21): activateTrainer(), archiveGlobalSupplement(), createGlobalExercise(), createGlobalFood(), createGlobalSupplement(), deleteGlobalExercise(), deleteGlobalFood(), deleteGlobalSupplement() (+13 more)

### Community 13 - "cn"
Cohesion: 0.17
Nodes (15): createPackType(), deletePackType(), getPackTypes(), updatePackType(), PackPurchaseDialog(), ButtonProps, buttonVariants, Calendar() (+7 more)

### Community 14 - "Domain.Entities.Training"
Cohesion: 0.09
Nodes (15): DateTime, Guid, ExerciseSet, DateTime, Guid, TrainingPlanDay, DateTime, Guid (+7 more)

### Community 15 - "AssessmentPage.jsx"
Cohesion: 0.17
Nodes (15): addCheckinNotes(), createAssessment(), createCheckin(), getAssessmentsByClient(), getCheckinsByClient(), skipCheckin(), updateAssessment(), ActivatePlanDialog() (+7 more)

### Community 16 - "compilerOptions"
Cohesion: 0.09
Nodes (22): compilerOptions, allowImportingTsExtensions, isolatedModules, jsx, lib, module, moduleDetection, moduleResolution (+14 more)

### Community 17 - "MealsPlanPage.jsx"
Cohesion: 0.20
Nodes (17): archiveMealPlan(), createFood(), createMealPlan(), deleteFood(), getFoods(), getMealPlansByClient(), unarchiveMealPlan(), updateFood() (+9 more)

### Community 18 - "trainingPlan.js"
Cohesion: 0.16
Nodes (11): createTrainingPlan(), deleteTrainingPlan(), getDayExercises(), getPlanDays(), getTrainingPlans(), setClientActivePlan(), updateTrainingPlan(), useTrainingPlans() (+3 more)

### Community 19 - "Exercises.jsx"
Cohesion: 0.26
Nodes (16): getGlobalExercises(), getGlobalFoods(), getGlobalSupplements(), AlertDialogAction, AlertDialogCancel, AlertDialogContent, AlertDialogDescription, AlertDialogFooter() (+8 more)

### Community 20 - "axiosConfig.js"
Cohesion: 0.13
Nodes (10): signupTrainer(), applyBrandColor(), AuthContext, AuthProvider(), hexToHSL(), api, failedQueue, createBillingPortal() (+2 more)

### Community 21 - "User"
Cohesion: 0.22
Nodes (6): DateTime, Guid, User, EmailAddress, Fact, UserTests

### Community 22 - "Clients.jsx"
Cohesion: 0.18
Nodes (16): createUser(), archiveClient(), createClient(), getClients(), unarchiveClient(), updateClient(), generateInvite(), purchasePack() (+8 more)

### Community 23 - "dependencies"
Cohesion: 0.11
Nodes (19): axios, dependencies, axios, lucide-react, @radix-ui/react-checkbox, react-day-picker, react-dom, recharts (+11 more)

### Community 24 - "Session"
Cohesion: 0.20
Nodes (9): DateOnly, DateTime, Guid, Session, Fact, SessionTests, Domain.Entities.Sessions, Domain.UnitTests.Entities.Sessions (+1 more)

### Community 25 - "net10.0"
Cohesion: 0.11
Nodes (18): dependencies, net10.0, type, contentHash, resolved, type, contentHash, resolved (+10 more)

### Community 26 - "Microsoft.Testing.Platform"
Cohesion: 0.11
Nodes (19): Microsoft.ApplicationInsights, Microsoft.Testing.Platform, Microsoft.ApplicationInsights, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type (+11 more)

### Community 27 - "net10.0"
Cohesion: 0.11
Nodes (18): dependencies, net10.0, type, contentHash, resolved, type, contentHash, resolved (+10 more)

### Community 28 - "Microsoft.Testing.Platform"
Cohesion: 0.11
Nodes (19): Microsoft.ApplicationInsights, Microsoft.Testing.Platform, Microsoft.ApplicationInsights, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type (+11 more)

### Community 29 - "net10.0"
Cohesion: 0.11
Nodes (18): contentHash, requested, resolved, type, dependencies, net10.0, type, contentHash (+10 more)

### Community 30 - "MyProfile.jsx"
Cohesion: 0.17
Nodes (16): getMyProfile(), formatProfileValue(), formatSexLabel(), formatTrainingModality(), InfoRow(), MyProfile(), confirmPasswordRules(), fullNameRules (+8 more)

### Community 31 - "Notification"
Cohesion: 0.24
Nodes (5): DateTime, Guid, Notification, Fact, NotificationTests

### Community 32 - "net10.0"
Cohesion: 0.11
Nodes (17): dependencies, net10.0, type, contentHash, resolved, type, contentHash, resolved (+9 more)

### Community 33 - "net10.0"
Cohesion: 0.11
Nodes (17): dependencies, net10.0, type, contentHash, resolved, type, contentHash, resolved (+9 more)

### Community 34 - "compilerOptions"
Cohesion: 0.11
Nodes (17): compilerOptions, allowImportingTsExtensions, isolatedModules, lib, module, moduleDetection, moduleResolution, noEmit (+9 more)

### Community 35 - "Client"
Cohesion: 0.21
Nodes (7): DateTime, Guid, Client, Fact, ClientTests, Domain.UnitTests.Entities.Clients, Domain.Entities.Clients

### Community 36 - "http"
Cohesion: 0.13
Nodes (15): ASPNETCORE_ENVIRONMENT, applicationUrl, commandName, dotnetRunMessages, environmentVariables, launchBrowser, applicationUrl, commandName (+7 more)

### Community 37 - "Microsoft.AspNetCore.OpenApi"
Cohesion: 0.13
Nodes (15): dependencies, type, Infrastructure, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi, Infrastructure, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi (+7 more)

### Community 38 - "Microsoft.AspNetCore.OpenApi"
Cohesion: 0.13
Nodes (15): dependencies, type, Infrastructure, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi, Infrastructure, Microsoft.AspNetCore.OpenApi, Microsoft.OpenApi (+7 more)

### Community 39 - "App.jsx"
Cohesion: 0.18
Nodes (11): calculateMacros(), getActivityFactors(), AdminLayout(), getInitials(), NavContent(), navItems, ClientDashboard(), LoginPage() (+3 more)

### Community 40 - "Supplement"
Cohesion: 0.21
Nodes (7): DateTime, Guid, Supplement, Fact, SupplementTests, Domain.UnitTests.Entities.Supplements, Domain.Entities.Supplements

### Community 41 - "xunit.v3.extensibility.core"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 42 - "xunit.v3.extensibility.core"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 43 - "xunit.v3.extensibility.core"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 44 - "xunit.v3.extensibility.core"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 45 - "xunit.v3.extensibility.core"
Cohesion: 0.14
Nodes (14): Microsoft.Win32.Registry, xunit.v3.common, Microsoft.Win32.Registry, xunit.v3.common, xunit.v3.extensibility.core, xunit.v3.runner.common, contentHash, dependencies (+6 more)

### Community 46 - "chart.tsx"
Cohesion: 0.14
Nodes (11): react, useCarousel(), ChartConfig, ChartContainer, ChartContext, ChartContextProps, ChartLegendContent, ChartTooltipContent (+3 more)

### Community 47 - "ClientSessionPack"
Cohesion: 0.29
Nodes (6): DateOnly, DateTime, Guid, ClientSessionPack, Fact, ClientSessionPackTests

### Community 48 - "dependencies"
Cohesion: 0.15
Nodes (13): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console (+5 more)

### Community 49 - "dependencies"
Cohesion: 0.15
Nodes (13): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console (+5 more)

### Community 50 - "dependencies"
Cohesion: 0.15
Nodes (13): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console (+5 more)

### Community 51 - "dependencies"
Cohesion: 0.15
Nodes (13): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console (+5 more)

### Community 52 - "dependencies"
Cohesion: 0.15
Nodes (13): Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console, Microsoft.Testing.Extensions.Telemetry, Microsoft.Testing.Extensions.TrxReport.Abstractions, Microsoft.Testing.Platform.MSBuild, xunit.v3.runner.inproc.console (+5 more)

### Community 53 - "devDependencies"
Cohesion: 0.15
Nodes (13): eslint, devDependencies, eslint, prettier, @testing-library/jest-dom, @types/node, typescript-eslint, @vitest/eslint-plugin (+5 more)

### Community 54 - "carousel.tsx"
Cohesion: 0.15
Nodes (12): Carousel, CarouselApi, CarouselContent, CarouselContext, CarouselContextProps, CarouselItem, CarouselNext, CarouselOptions (+4 more)

### Community 55 - "PackType"
Cohesion: 0.29
Nodes (5): DateTime, Guid, PackType, Fact, PackTypeTests

### Community 56 - "Food"
Cohesion: 0.29
Nodes (5): DateTime, Guid, Food, Fact, FoodTests

### Community 57 - "ClientExerciseSetLog"
Cohesion: 0.23
Nodes (6): DateTime, Guid, ClientExerciseSetLog, InlineData, Theory, ClientExerciseSetLogTests

### Community 58 - "TrainingPlan"
Cohesion: 0.24
Nodes (6): DateOnly, DateTime, Guid, TrainingPlan, Fact, TrainingPlanTests

### Community 59 - "Microsoft.Testing.Platform"
Cohesion: 0.17
Nodes (12): Microsoft.Testing.Platform, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type, contentHash, dependencies (+4 more)

### Community 60 - "Microsoft.Testing.Platform"
Cohesion: 0.17
Nodes (12): Microsoft.Testing.Platform, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type, contentHash, dependencies (+4 more)

### Community 61 - "Microsoft.Testing.Platform"
Cohesion: 0.17
Nodes (12): Microsoft.Testing.Platform, Microsoft.Testing.Platform, contentHash, dependencies, resolved, type, contentHash, dependencies (+4 more)

### Community 62 - "supplementApi.js"
Cohesion: 0.23
Nodes (6): archiveSupplement(), createSupplement(), deleteSupplement(), unarchiveSupplement(), updateSupplement(), SupplementsPage()

### Community 63 - "Domain.Entities.Billing"
Cohesion: 0.22
Nodes (5): DateTime, Guid, ProcessedStripeEvent, Domain.UnitTests.Entities.Billing, Domain.Entities.Billing

### Community 64 - "RefreshToken"
Cohesion: 0.31
Nodes (5): DateTime, Guid, RefreshToken, Fact, RefreshTokenTests

### Community 65 - "net10.0"
Cohesion: 0.18
Nodes (10): dependencies, type, dependencies, Domain, net10.0, type, Domain, application (+2 more)

### Community 66 - "xunit.v3.mtp-v1"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 67 - "xunit.v3.mtp-v1"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 68 - "xunit.v3.mtp-v1"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 69 - "xunit.v3.mtp-v1"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 70 - "xunit.v3.mtp-v1"
Cohesion: 0.18
Nodes (11): xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.analyzers, xunit.v3.assert, xunit.v3.core.mtp-v1, xunit.v3.mtp-v1, contentHash (+3 more)

### Community 71 - "PlanDaysList.jsx"
Cohesion: 0.33
Nodes (9): addExerciseToDay(), createPlanDay(), deleteDayExercise(), deletePlanDay(), updateDayExercise(), PlanDaysList(), AccordionContent, AccordionItem (+1 more)

### Community 72 - "Application"
Cohesion: 0.20
Nodes (10): dependencies, type, Application, Domain, dependencies, type, Application, Domain (+2 more)

### Community 73 - "Microsoft.NET.Test.Sdk"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 74 - "Application"
Cohesion: 0.20
Nodes (10): dependencies, type, Application, Domain, dependencies, type, Application, Domain (+2 more)

### Community 75 - "Microsoft.NET.Test.Sdk"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 76 - "Domain"
Cohesion: 0.20
Nodes (10): dependencies, type, Application, Domain, dependencies, type, Application, Domain (+2 more)

### Community 77 - "Microsoft.NET.Test.Sdk"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 78 - "Microsoft.NET.Test.Sdk"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 79 - "Microsoft.NET.Test.Sdk"
Cohesion: 0.20
Nodes (10): Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, Microsoft.CodeCoverage, Microsoft.TestPlatform.TestHost, contentHash, dependencies, requested, resolved (+2 more)

### Community 80 - "scripts"
Cohesion: 0.20
Nodes (10): scripts, build, dev, format, format:check, lint, preview, test (+2 more)

### Community 81 - "exercisesApi.js"
Cohesion: 0.31
Nodes (7): createExercise(), deleteExercise(), getExercises(), updateExercise(), ExercisePicker(), useExercises(), Exercises()

### Community 82 - "command.tsx"
Cohesion: 0.20
Nodes (8): Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList, CommandSeparator, CommandShortcut()

### Community 83 - "context-menu.tsx"
Cohesion: 0.20
Nodes (9): ContextMenuCheckboxItem, ContextMenuContent, ContextMenuItem, ContextMenuLabel, ContextMenuRadioItem, ContextMenuSeparator, ContextMenuShortcut(), ContextMenuSubContent (+1 more)

### Community 84 - "InviteToken"
Cohesion: 0.36
Nodes (5): DateTime, Guid, InviteToken, Fact, InviteTokenTests

### Community 85 - "Exercise"
Cohesion: 0.36
Nodes (4): DateTime, Guid, Exercise, Fact

### Community 86 - "SubscriptionStatus"
Cohesion: 0.33
Nodes (6): SubscriptionStatus, Fact, MemberData, Theory, TheoryData, SubscriptionStatusTests

### Community 87 - "xunit.v3.runner.inproc.console"
Cohesion: 0.22
Nodes (9): xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, contentHash, dependencies, resolved (+1 more)

### Community 88 - "xunit.v3.runner.inproc.console"
Cohesion: 0.22
Nodes (9): xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, contentHash, dependencies, resolved (+1 more)

### Community 89 - "xunit.v3.runner.inproc.console"
Cohesion: 0.22
Nodes (9): xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, contentHash, dependencies, resolved (+1 more)

### Community 90 - "xunit.v3.runner.inproc.console"
Cohesion: 0.22
Nodes (9): xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, contentHash, dependencies, resolved (+1 more)

### Community 91 - "xunit.v3.runner.inproc.console"
Cohesion: 0.22
Nodes (9): xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.extensibility.core, xunit.v3.runner.common, xunit.v3.runner.inproc.console, contentHash, dependencies, resolved (+1 more)

### Community 92 - "ITenantContext.cs"
Cohesion: 0.29
Nodes (6): IClock, DateTime, ITenantContext, TenantOrigin, Guid, Application.Common.Abstractions

### Community 93 - "TrainerSettings"
Cohesion: 0.36
Nodes (4): DateTime, Guid, TrainerSettings, Domain.Entities.TrainerSettings

### Community 94 - "xunit.v3"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 95 - "xunit.v3"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 96 - "xunit.v3"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 97 - "xunit.v3"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 98 - "xunit.v3"
Cohesion: 0.25
Nodes (8): xunit.v3.mtp-v1, xunit.v3.mtp-v1, xunit.v3, contentHash, dependencies, requested, resolved, type

### Community 99 - "EmailAddressTests"
Cohesion: 0.32
Nodes (4): Fact, InlineData, Theory, EmailAddressTests

### Community 100 - "breadcrumb.tsx"
Cohesion: 0.25
Nodes (7): Breadcrumb, BreadcrumbEllipsis(), BreadcrumbItem, BreadcrumbLink, BreadcrumbList, BreadcrumbPage, BreadcrumbSeparator()

### Community 101 - "drawer.tsx"
Cohesion: 0.25
Nodes (6): DrawerContent, DrawerDescription, DrawerFooter(), DrawerHeader(), DrawerOverlay, DrawerTitle

### Community 102 - "xunit.v3.common"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 103 - "Microsoft.TestPlatform.TestHost"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 104 - "xunit.v3.common"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 105 - "Microsoft.TestPlatform.TestHost"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 106 - "Microsoft.Testing.Extensions.Telemetry"
Cohesion: 0.29
Nodes (7): Microsoft.ApplicationInsights, Microsoft.ApplicationInsights, contentHash, dependencies, resolved, type, Microsoft.Testing.Extensions.Telemetry

### Community 107 - "xunit.v3.common"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 108 - "Microsoft.TestPlatform.TestHost"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 109 - "Microsoft.Testing.Extensions.Telemetry"
Cohesion: 0.29
Nodes (7): Microsoft.ApplicationInsights, Microsoft.ApplicationInsights, contentHash, dependencies, resolved, type, Microsoft.Testing.Extensions.Telemetry

### Community 110 - "xunit.v3.common"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 111 - "Microsoft.TestPlatform.TestHost"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 112 - "Microsoft.Testing.Extensions.Telemetry"
Cohesion: 0.29
Nodes (7): Microsoft.ApplicationInsights, Microsoft.ApplicationInsights, contentHash, dependencies, resolved, type, Microsoft.Testing.Extensions.Telemetry

### Community 113 - "xunit.v3.common"
Cohesion: 0.29
Nodes (7): Microsoft.Bcl.AsyncInterfaces, Microsoft.Bcl.AsyncInterfaces, xunit.v3.common, contentHash, dependencies, resolved, type

### Community 114 - "Microsoft.TestPlatform.TestHost"
Cohesion: 0.29
Nodes (7): Microsoft.TestPlatform.ObjectModel, Microsoft.TestPlatform.ObjectModel, contentHash, dependencies, resolved, type, Microsoft.TestPlatform.TestHost

### Community 115 - "inviteApi.js"
Cohesion: 0.52
Nodes (5): setPasswordViaInvite(), validateInvite(), ClientFirstLoginPage(), renderInvitePage(), renderWithValidToken()

### Community 116 - "badge.tsx"
Cohesion: 0.38
Nodes (5): MUSCLE_GROUPS, MuscleMultiSelect(), Badge(), BadgeProps, badgeVariants

### Community 117 - "packages.lock.json"
Cohesion: 0.33
Nodes (5): dependencies, net10.0, type, domain, version

### Community 118 - "OutboxMessage"
Cohesion: 0.53
Nodes (3): DateTime, Guid, OutboxMessage

### Community 120 - "coverlet.collector"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, coverlet.collector

### Community 121 - "Microsoft.OpenApi"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, Microsoft.OpenApi

### Community 122 - "coverlet.collector"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, coverlet.collector

### Community 123 - "Microsoft.OpenApi"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, Microsoft.OpenApi

### Community 124 - "xunit.runner.visualstudio"
Cohesion: 0.40
Nodes (5): xunit.runner.visualstudio, contentHash, requested, resolved, type

### Community 125 - "application"
Cohesion: 0.40
Nodes (5): dependencies, type, Domain, Domain, application

### Community 126 - "coverlet.collector"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, coverlet.collector

### Community 127 - "xunit.runner.visualstudio"
Cohesion: 0.40
Nodes (5): xunit.runner.visualstudio, contentHash, requested, resolved, type

### Community 128 - "coverlet.collector"
Cohesion: 0.40
Nodes (5): contentHash, requested, resolved, type, coverlet.collector

### Community 129 - "xunit.runner.visualstudio"
Cohesion: 0.40
Nodes (5): xunit.runner.visualstudio, contentHash, requested, resolved, type

### Community 131 - "package.json"
Cohesion: 0.40
Nodes (4): name, private, type, version

### Community 132 - "alert.tsx"
Cohesion: 0.40
Nodes (4): Alert, AlertDescription, AlertTitle, alertVariants

### Community 133 - "packages.lock.json"
Cohesion: 0.50
Nodes (3): dependencies, net10.0, version

### Community 134 - "Microsoft.ApplicationInsights"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.ApplicationInsights

### Community 135 - "Microsoft.CodeCoverage"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.CodeCoverage

### Community 136 - "Microsoft.Testing.Platform"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 137 - "Microsoft.TestPlatform.ObjectModel"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.TestPlatform.ObjectModel

### Community 138 - "xunit.v3.assert"
Cohesion: 0.50
Nodes (4): xunit.v3.assert, contentHash, resolved, type

### Community 139 - "xunit.v3.assert"
Cohesion: 0.50
Nodes (4): xunit.v3.assert, contentHash, resolved, type

### Community 140 - "Microsoft.ApplicationInsights"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.ApplicationInsights

### Community 141 - "xunit.analyzers"
Cohesion: 0.50
Nodes (4): xunit.analyzers, contentHash, resolved, type

### Community 142 - "Microsoft.CodeCoverage"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.CodeCoverage

### Community 143 - "Microsoft.Testing.Platform"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 144 - "Microsoft.TestPlatform.ObjectModel"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.TestPlatform.ObjectModel

### Community 145 - "xunit.v3.assert"
Cohesion: 0.50
Nodes (4): xunit.v3.assert, contentHash, resolved, type

### Community 146 - "Microsoft.ApplicationInsights"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.ApplicationInsights

### Community 147 - "Microsoft.Testing.Platform"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 148 - "Microsoft.TestPlatform.ObjectModel"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.TestPlatform.ObjectModel

### Community 149 - "Microsoft.Win32.Registry"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Win32.Registry

### Community 150 - "xunit.analyzers"
Cohesion: 0.50
Nodes (4): xunit.analyzers, contentHash, resolved, type

### Community 151 - "xunit.analyzers"
Cohesion: 0.50
Nodes (4): xunit.analyzers, contentHash, resolved, type

### Community 152 - "Microsoft.Bcl.AsyncInterfaces"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Bcl.AsyncInterfaces

### Community 153 - "Microsoft.Testing.Platform"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 154 - "Microsoft.TestPlatform.ObjectModel"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.TestPlatform.ObjectModel

### Community 155 - "Microsoft.Win32.Registry"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Win32.Registry

### Community 156 - "xunit.analyzers"
Cohesion: 0.50
Nodes (4): xunit.analyzers, contentHash, resolved, type

### Community 157 - "Microsoft.CodeCoverage"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.CodeCoverage

### Community 158 - "Microsoft.Testing.Platform"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.Testing.Platform

### Community 159 - "Microsoft.TestPlatform.ObjectModel"
Cohesion: 0.50
Nodes (4): contentHash, resolved, type, Microsoft.TestPlatform.ObjectModel

### Community 160 - "xunit.analyzers"
Cohesion: 0.50
Nodes (4): xunit.analyzers, contentHash, resolved, type

### Community 161 - "xunit.v3.assert"
Cohesion: 0.50
Nodes (4): xunit.v3.assert, contentHash, resolved, type

## Knowledge Gaps
- **759 isolated node(s):** `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi`, `Microsoft.NET.Sdk.Web`, `$schema`, `commandName` (+754 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **44 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `Domain.Exceptions` connect `Domain.Exceptions` to `DomainException.cs`, `Client`, `MealPlan`, `Supplement`, `Checkin`, `Domain.Entities.Training`, `Domain.Entities.Nutrition`, `Session`, `ClientExerciseSetLog`, `TrainingPlan`, `TrainerSettings`, `Domain.Entities.Billing`?**
  _High betweenness centrality (0.042) - this node is a cross-community bridge._
- **Why does `dependencies` connect `dependencies` to `package.json`, `clsx`, `cmdk`, `embla-carousel-react`, `class-variance-authority`, `@radix-ui/react-accordion`, `@radix-ui/react-alert-dialog`, `@radix-ui/react-aspect-ratio`, `chart.tsx`, `@radix-ui/react-avatar`, `@radix-ui/react-context-menu`, `@radix-ui/react-dropdown-menu`, `@radix-ui/react-label`, `@radix-ui/react-progress`, `@radix-ui/react-select`, `@radix-ui/react-separator`, `@radix-ui/react-slot`, `@radix-ui/react-switch`, `@radix-ui/react-tabs`, `react-hook-form`, `react-router-dom`, `react-toastify`, `tailwind-merge`?**
  _High betweenness centrality (0.027) - this node is a cross-community bridge._
- **Why does `react` connect `chart.tsx` to `dependencies`?**
  _High betweenness centrality (0.025) - this node is a cross-community bridge._
- **What connects `Microsoft.AspNetCore.OpenApi`, `Microsoft.OpenApi`, `Microsoft.NET.Sdk.Web` to the rest of the system?**
  _762 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Sessions.jsx` be split into smaller, more focused modules?**
  _Cohesion score 0.0882936507936508 - nodes in this community are weakly interconnected._
- **Should `useAuth` be split into smaller, more focused modules?**
  _Cohesion score 0.06328320802005012 - nodes in this community are weakly interconnected._
- **Should `PTManager.sln` be split into smaller, more focused modules?**
  _Cohesion score 0.06090808416389812 - nodes in this community are weakly interconnected._