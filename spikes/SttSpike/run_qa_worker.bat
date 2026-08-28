@echo off
cd /d C:\Dev\HomeQuranLearning.QA\spikes\SttSpike
set PYTHONUNBUFFERED=1
set PYTHONUTF8=1
set PYTHONIOENCODING=utf-8
call .venv\Scripts\activate.bat
python -u qa_worker.py
