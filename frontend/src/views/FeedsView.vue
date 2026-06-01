<template>
  <div class="feeds-page">
    <div class="page-head">
      <p>Adminbereich</p>
      <h1>RSS-Quellen</h1>
    </div>

    <div v-if="error" class="alert alert-danger">
      {{ error }}
    </div>

    <section class="feed-form">
      <input v-model="name" placeholder="Name, z.B. ORF" />
      <input v-model="url" placeholder="RSS-URL" />
      <button @click="createFeed">Hinzufügen</button>
    </section>

    <div v-if="loading" class="status-box">
      Feeds werden geladen...
    </div>

    <section class="feed-list">
      <article v-for="feed in feeds" :key="feed.id" class="feed-card">
        <div>
          <strong>{{ feed.name }}</strong>
          <a :href="feed.url" target="_blank">{{ feed.url }}</a>
        </div>

        <div class="feed-actions">
          <span :class="feed.isActive ? 'active' : 'inactive'">
            {{ feed.isActive ? "Aktiv" : "Inaktiv" }}
          </span>

          <button v-if="!feed.isActive" @click="activateFeed(feed.id)">
            Aktivieren
          </button>

          <button v-if="feed.isActive" @click="deactivateFeed(feed.id)">
            Deaktivieren
          </button>

          <button class="danger" @click="deleteFeed(feed.id)">
            Löschen
          </button>
        </div>
      </article>
    </section>
  </div>
</template>

<script setup>
import { onMounted, ref } from "vue";
import api from "../api";

const feeds = ref([]);
const name = ref("");
const url = ref("");
const loading = ref(false);
const error = ref("");

async function loadFeeds() {
  loading.value = true;
  error.value = "";

  try {
    const response = await api.get("/rssfeeds");
    feeds.value = response.data;
  } catch (err) {
    console.error(err);

    if (err.response?.status === 403) {
      error.value = "Nur admin darf RSS-Quellen verwalten.";
    } else if (err.response?.status === 401) {
      error.value = "Bitte als admin einloggen.";
    } else {
      error.value = "Feeds konnten nicht geladen werden.";
    }
  } finally {
    loading.value = false;
  }
}

async function createFeed() {
  error.value = "";

  if (!name.value.trim() || !url.value.trim()) {
    error.value = "Name und URL sind Pflicht.";
    return;
  }

  try {
    await api.post("/rssfeeds", {
      name: name.value,
      url: url.value,
    });

    name.value = "";
    url.value = "";

    await loadFeeds();
  } catch (err) {
    console.error(err);
    error.value = "Feed konnte nicht erstellt werden.";
  }
}

async function activateFeed(id) {
  await api.put(`/rssfeeds/${id}/activate`);
  await loadFeeds();
}

async function deactivateFeed(id) {
  await api.put(`/rssfeeds/${id}/deactivate`);
  await loadFeeds();
}

async function deleteFeed(id) {
  if (!confirm("Feed wirklich löschen?")) return;

  await api.delete(`/rssfeeds/${id}`);
  await loadFeeds();
}

onMounted(loadFeeds);
</script>

<style scoped>
.feeds-page {
  max-width: 1100px;
  margin: 0 auto;
  padding: 2rem 1.5rem 4rem;
}

.page-head p {
  color: #198754;
  font-size: 0.78rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.16em;
  margin: 0 0 0.4rem;
}

.page-head h1 {
  font-size: 4rem;
  font-weight: 950;
  letter-spacing: -0.07em;
  margin-bottom: 2rem;
}

.feed-form {
  display: grid;
  grid-template-columns: 1fr 2fr auto;
  gap: 0.75rem;
  margin-bottom: 2rem;
}

.feed-form input {
  border: 1px solid #d1d5db;
  border-radius: 14px;
  padding: 0.9rem 1rem;
}

.feed-form button {
  border: 0;
  background: #198754;
  color: white;
  border-radius: 999px;
  padding: 0.9rem 1.2rem;
  font-weight: 900;
}

.status-box {
  padding: 1rem;
  border-radius: 14px;
  background: white;
  border: 1px solid #e5e7eb;
}

.feed-list {
  display: grid;
  gap: 0.8rem;
}

.feed-card {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  background: white;
  border: 1px solid #e5e7eb;
  border-radius: 18px;
  padding: 1rem;
}

.feed-card strong {
  display: block;
  font-weight: 950;
}

.feed-card a {
  display: block;
  color: #6b7280;
  word-break: break-all;
}

.feed-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.feed-actions span {
  padding: 0.45rem 0.7rem;
  border-radius: 999px;
  font-weight: 850;
  font-size: 0.8rem;
}

.feed-actions .active {
  background: #d1fae5;
  color: #065f46;
}

.feed-actions .inactive {
  background: #e5e7eb;
  color: #374151;
}

.feed-actions button {
  border: 1px solid #d1d5db;
  background: white;
  border-radius: 999px;
  padding: 0.55rem 0.8rem;
  font-weight: 850;
}

.feed-actions .danger {
  background: #dc3545;
  color: white;
  border-color: #dc3545;
}

@media (max-width: 780px) {
  .feed-form {
    grid-template-columns: 1fr;
  }

  .feed-card {
    flex-direction: column;
  }

  .page-head h1 {
    font-size: 3rem;
  }
}
</style>