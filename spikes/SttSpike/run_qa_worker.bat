@echo off
cd /d C:\Dev\HomeQuranLearning.QA\spikes\SttSpike
set PYTHONUNBUFFERED=1
call .venv\Scripts\activate.bat
python -u qa_worker.py