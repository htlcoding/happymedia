<template>
  <div class="auth-page">
    <div class="auth-card">
      <p class="eyebrow">HappyMedia</p>
      <h1>Registrieren</h1>

      <div v-if="error" class="alert alert-danger">
        {{ error }}
      </div>

      <form @submit.prevent="register">
        <label>Username</label>
        <input v-model="username" />

        <label>Passwort</label>
        <input v-model="password" type="password" />

        <button :disabled="loading">
          {{ loading ? "Wird erstellt..." : "Konto erstellen" }}
        </button>
      </form>

      <p class="switch-text">
        Schon registriert?
        <RouterLink to="/login">Zum Login</RouterLink>
      </p>
    </div>
  </div>
</template>

<script setup>
import { ref } from "vue";
import { useRouter } from "vue-router";
import api from "../api";
import { setAuth } from "../auth";

const router = useRouter();

const username = ref("");
const password = ref("");
const error = ref("");
const loading = ref(false);

async function register() {
  error.value = "";

  if (password.value.length < 8) {
    error.value = "Das Passwort muss mindestens 8 Zeichen lang sein.";
    return;
  }

  loading.value = true;

  try {
    const response = await api.post("/auth/register", {
      username: username.value,
      password: password.value,
    });

    setAuth(response.data);
    router.push("/");
  } catch (err) {
    console.error(err);
    error.value = "Registrierung fehlgeschlagen.";
  } finally {
    loading.value = false;
  }
}
</script>

<style scoped>
.auth-page {
  min-height: calc(100vh - 90px);
  display: grid;
  place-items: center;
  background: #f5f5f2;
  padding: 2rem;
}

.auth-card {
  width: 100%;
  max-width: 460px;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 26px;
  padding: 2rem;
}

.eyebrow {
  color: #198754;
  font-size: 0.78rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.16em;
}

h1 {
  font-size: 3rem;
  font-weight: 950;
  letter-spacing: -0.06em;
  margin-bottom: 1.5rem;
}

label {
  display: block;
  margin-bottom: 0.4rem;
  font-weight: 850;
}

input {
  width: 100%;
  border: 1px solid #d1d5db;
  border-radius: 14px;
  padding: 0.9rem 1rem;
  margin-bottom: 1rem;
}

button {
  width: 100%;
  border: 0;
  border-radius: 999px;
  background: #198754;
  color: white;
  padding: 0.9rem 1rem;
  font-weight: 900;
}

.switch-text {
  margin-top: 1rem;
  margin-bottom: 0;
}
</style>