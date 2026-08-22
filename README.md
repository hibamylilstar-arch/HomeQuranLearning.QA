\# HomeQuranLearning QA



Private teacher monitoring and QA system for HomeQuranLearning academy.



\## Components



\- Windows Agent — .NET 10 Worker Service

\- Backend API — ASP.NET Core, PostgreSQL, MinIO

\- QA Worker — Python faster-whisper

\- Dashboard — Next.js + TypeScript + Tailwind CSS



\## Development Setup



1\. Start infrastructure:



&#x20;  ```powershell

&#x20;  cd infrastructure\\docker

&#x20;  docker compose up -d

