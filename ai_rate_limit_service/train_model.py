import pickle
from sklearn.tree import DecisionTreeRegressor
import numpy as np

# Example: X = [[hour_of_day, request_count]], y = [rate_limit]
X = np.array([[0, 5], [12, 20], [18, 50], [23, 10]])
y = np.array([5, 20, 50, 10])

model = DecisionTreeRegressor()
model.fit(X, y)

# Save the model
with open("rate_limit_model.pkl", "wb") as f:
    pickle.dump(model, f)