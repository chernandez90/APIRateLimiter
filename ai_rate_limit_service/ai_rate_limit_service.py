from fastapi import FastAPI
from pydantic import BaseModel
import pickle
import numpy as np

app = FastAPI()

# Load the trained model
with open("rate_limit_model.pkl", "rb") as f:
    model = pickle.load(f)

class RateLimitRequest(BaseModel):
    ip: str
    hour_of_day: int
    request_count: int

@app.post("/predict-limit")
async def predict_limit(req: RateLimitRequest):
    # Example logic: lower limit at night, higher during the day, or based on request_count
    if req.hour_of_day < 6 or req.hour_of_day > 22:
        rate_limit = 3  # Low limit at night
    elif req.request_count > 5:
        rate_limit = 2  # Very strict if user is making many requests
    else:
        rate_limit = 10  # Default
    print(f"AI Service: ip={req.ip}, hour={req.hour_of_day}, count={req.request_count} => limit={rate_limit}")
    return {"rate_limit": rate_limit}