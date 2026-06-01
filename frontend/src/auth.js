import { computed, reactive } from "vue";

function readUser() {
  try {
    return JSON.parse(localStorage.getItem("user") || "null");
  } catch {
    return null;
  }
}

export const authState = reactive({
  token: localStorage.getItem("token"),
  user: readUser(),
});

export const isLoggedIn = computed(() => !!authState.token);

export const isAdmin = computed(() => {
  return authState.user?.username?.toLowerCase() === "admin";
});

export function setAuth(data) {
  localStorage.setItem("token", data.token);
  localStorage.setItem(
    "user",
    JSON.stringify({
      id: data.id,
      username: data.username,
    })
  );

  authState.token = data.token;
  authState.user = {
    id: data.id,
    username: data.username,
  };
}

export function clearAuth() {
  localStorage.removeItem("token");
  localStorage.removeItem("user");

  authState.token = null;
  authState.user = null;
}