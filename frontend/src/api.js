import axios from "axios";

const api = axios.create({
  baseURL: "https://localhost:7045/api",
});

api.interceptors.request.use((config) => {
  const token =
    localStorage.getItem("token") ||
    localStorage.getItem("happypedia-token") ||
    localStorage.getItem("happymedia-token");

  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }

  return config;
});

export default api;