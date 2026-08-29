FROM python:3.11-slim

WORKDIR /app

# Install dependencies
COPY src/LiveKitIngressManager/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

# Copy the worker script
COPY src/LiveKitIngressManager/ingress_manager.py .

# Ensure Python output is immediately printed to Docker logs
ENV PYTHONUNBUFFERED=1

# Run the script continuously
CMD ["python", "ingress_manager.py"]
