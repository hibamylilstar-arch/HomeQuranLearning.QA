\# HomeQuranLearning QA — Current Architecture



\## Overview



HomeQuranLearning QA is a private teacher monitoring and QA system for an online Quran academy.



The system consists of:



\- Windows Agent on academy-managed teacher laptops

\- Cloud backend API

\- Web dashboard

\- QA speech-to-text worker



\---



\## Components



\### Windows Agent



\- C# .NET 10 Worker Service

\- Runs on teacher laptops

\- Captures screen via DirectX OutputDuplication

\- Captures system audio via NAudio WASAPI loopback

\- Records MP4 using FFmpeg post-processing

\- Handles silent audio gracefully

\- Sends device heartbeat

\- Uploads recording metadata and MP4 to backend

\- Runs as a Windows service



\### Backend API



\- ASP.NET Core minimal APIs

\- PostgreSQL via EF Core

\- Redis available for future caching/pub-sub

\- MinIO object storage for recordings

\- JWT authentication for dashboard users

\- API key authentication for agent and QA worker

\- RBAC: Owner, Admin, Manager

\- Automatic session creation from schedules

\- Recording/session association

\- Secure presigned playback URLs



\### QA Worker



\- Python faster-whisper

\- Polls backend for unprocessed uploaded recordings

\- Downloads MP4 from MinIO

\- Transcribes audio locally

\- Matches active QA rules

\- Creates QA alerts automatically



\### Dashboard



\- Next.js + TypeScript + Tailwind CSS

\- Responsive desktop/mobile layout

\- Login, devices, recordings, QA rules/alerts

\- Teacher/Student/Course/Schedule/Session management

\- Manager assignment and filtering

\- Secure inline video player



\---



\## Database Tables



\- users

\- teachers

\- students

\- courses

\- devices

\- device\_heartbeats

\- schedules

\- sessions

\- recordings

\- qa\_rules

\- qa\_alerts

\- manager\_teacher\_assignments

\- \_\_EFMigrationsHistory



\---



\## Key Relationships



\- Recording -> Device (Cascade)

\- Recording -> Teacher (SetNull)

\- Recording -> Session (SetNull)

\- Recording -> QaAlert (Cascade)

\- QaAlert -> QaRule (SetNull)

\- User -> ManagerTeacherAssignment

\- Teacher -> ManagerTeacherAssignment

\- Teacher -> Student (SetNull)

\- Schedule -> Teacher, Student, Course, Device

\- Session -> Teacher, Student, Course, Device

\- Session -> Schedule (SetNull)



\---



\## Authentication / Authorization



\- Dashboard uses JWT bearer tokens.

\- Agent uses `X-Api-Key` header.

\- QA worker uses `X-Api-Key` header with worker key.

\- Admin endpoints require JWT and role checks.

\- Manager resource filtering:

&#x20; - Recordings filtered by assigned teachers.

&#x20; - QA alerts filtered through visible recordings.

&#x20; - Devices filtered by assigned teachers' sessions.



\---



\## Configuration



\- Development settings are in `appsettings.Development.json`.

\- Production secrets are placeholders in `appsettings.json`.

\- Docker `.env` contains local infrastructure secrets.

\- Production must use environment variables or a secure secret store.



\---



\## Automated Tests



\- Located in `tests/Academy.UnitTests`

\- Cover:

&#x20; - AuthService login

&#x20; - PasswordHasher

&#x20; - Recording/session association

&#x20; - Manager filtering for recordings

&#x20; - Manager filtering for devices



Run tests with:



```powershell

dotnet test tests\\Academy.UnitTests\\Academy.UnitTests.csproj

