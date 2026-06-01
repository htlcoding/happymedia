<template>
  <main class="article-detail-page">
    <section v-if="loading" class="status-box">
      Artikel wird geladen...
    </section>

    <section v-if="error" class="status-box error">
      {{ error }}
    </section>

    <section v-if="translationLoading" class="status-box translation-info">
      Artikel wird automatisch übersetzt...
    </section>

    <article v-if="article && !loading" class="article-shell">
      <RouterLink to="/" class="back-link">
        ← Zurück zur Übersicht
      </RouterLink>

      <header class="article-header">
        <div class="article-meta">
          <span>{{ article.source }}</span>
          <span>Score {{ article.score }}</span>
          <span>{{ formatDate(article.publishedAt) }}</span>
        </div>

        <h1>{{ articleTitle }}</h1>


        <p v-if="isTranslated" class="translation-note">
          Automatisch übersetzt
        </p>
      </header>

      <div v-if="article.imageUrl" class="article-image-wrap">
        <img
          :src="article.imageUrl"
          :alt="articleTitle"
          class="article-image"
        />
      </div>

      <section class="article-content">
        <p v-if="articleContent">
          {{ articleContent }}
        </p>

        <p v-else-if="articleSummary">
          {{ articleSummary }}
        </p>
      </section>

      <section class="article-actions">
        <button
          class="btn vote-button"
          :class="likeButtonClass"
          :disabled="actionLoading"
          @click="likeArticle"
        >
          👍 Like {{ article.upvotes ?? 0 }}
        </button>

        <button
          class="btn vote-button"
          :class="dislikeButtonClass"
          :disabled="actionLoading"
          @click="dislikeArticle"
        >
          👎 Dislike {{ article.downvotes ?? 0 }}
        </button>

        <a
          v-if="article.url"
          :href="article.url"
          target="_blank"
          rel="noopener noreferrer"
          class="source-button"
        >
          Originalquelle öffnen
        </a>
      </section>

      <section class="comments-section">
        <div class="comments-head">
          <h2>Kommentare</h2>
          <span>{{ comments.length }}</span>
        </div>

        <form v-if="isLoggedIn" class="comment-form" @submit.prevent="submitComment">
          <textarea
            v-model="newComment"
            placeholder="Schreibe einen Kommentar..."
            rows="4"
          ></textarea>

          <button type="submit" :disabled="commentLoading || !newComment.trim()">
            {{ commentLoading ? "Wird gesendet..." : "Kommentar senden" }}
          </button>
        </form>

        <div v-else class="login-hint">
          Bitte einloggen, um zu kommentieren.
        </div>

        <div v-if="comments.length > 0" class="comment-list">
          <article
            v-for="comment in comments"
            :key="comment.id"
            class="comment-card"
          >
            <div class="comment-meta">
              <strong>{{ comment.username || comment.userName || "User" }}</strong>
              <span>{{ formatDate(comment.createdAt) }}</span>
            </div>

            <p>{{ comment.text || comment.content }}</p>
          </article>
        </div>

        <div v-else class="empty-comments">
          Noch keine Kommentare vorhanden.
        </div>
      </section>
    </article>
  </main>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import { RouterLink, useRoute } from "vue-router";
import api from "../api";
import { isLoggedIn } from "../auth";
import {
  createGermanTranslator,
  getDetailTranslationStorageKey,
  getPreviewTranslationStorageKey,
  isRealTranslation,
  loadTranslationFromStorage,
  looksEnglish,
  saveTranslationToStorage,
  translateTextToGerman,
} from "../translateBrowser";

const route = useRoute();

const articleId = route.params.id;

const article = ref(null);
const comments = ref([]);

const loading = ref(false);
const error = ref("");
const actionLoading = ref(false);
const commentLoading = ref(false);

const newComment = ref("");
const userVote = ref(null);

const translatedArticle = ref(null);
const translationLoading = ref(false);
const isTranslated = ref(false);

const voteStorageKey = `happymedia-vote-${articleId}`;

const articleTitle = computed(() => {
  return translatedArticle.value?.title || article.value?.title || "";
});

const articleSummary = computed(() => {
  return translatedArticle.value?.summary || article.value?.summary || "";
});

const articleContent = computed(() => {
  return translatedArticle.value?.content || article.value?.content || "";
});

const likeButtonClass = computed(() => {
  return userVote.value === "like" ? "btn-success" : "btn-outline-secondary";
});

const dislikeButtonClass = computed(() => {
  return userVote.value === "dislike" ? "btn-danger" : "btn-outline-secondary";
});

async function loadArticle() {
  loading.value = true;
  error.value = "";

  try {
    const response = await api.get(`/articles/${articleId}`);
    article.value = response.data;

    loadSavedVote();
    loadSavedTranslation();

    if (!hasFullTranslation() && looksEnglish(article.value)) {
      translateArticleDetailAutomatically();
    }
  } catch (err) {
    console.error(err);

    if (err.response?.status === 404) {
      error.value = "Artikel wurde nicht gefunden.";
    } else if (err.response) {
      error.value = `Artikel konnte nicht geladen werden. Status: ${err.response.status}`;
    } else {
      error.value = "Backend nicht erreichbar.";
    }
  } finally {
    loading.value = false;
  }
}

async function loadComments() {
  try {
    const response = await api.get(`/articles/${articleId}/comments`);
    comments.value = Array.isArray(response.data) ? response.data : [];
  } catch (err) {
    console.warn("Kommentare konnten nicht geladen werden:", err);
    comments.value = [];
  }
}

function loadSavedTranslation() {
  if (!article.value) {
    return;
  }

  const detailTranslation = loadTranslationFromStorage(
    getDetailTranslationStorageKey(article.value)
  );

  if (detailTranslation) {
    const titleChanged = isRealTranslation(article.value.title, detailTranslation.title);
    const summaryChanged = isRealTranslation(article.value.summary, detailTranslation.summary);
    const contentChanged = isRealTranslation(article.value.content, detailTranslation.content);

    if (titleChanged || summaryChanged || contentChanged) {
      translatedArticle.value = {
        title: detailTranslation.title || article.value.title || "",
        summary: detailTranslation.summary || article.value.summary || "",
        content: detailTranslation.content || article.value.content || "",
      };

      isTranslated.value = true;
      return;
    }
  }

  const previewTranslation = loadTranslationFromStorage(
    getPreviewTranslationStorageKey(article.value)
  );

  if (previewTranslation) {
    const titleChanged = isRealTranslation(article.value.title, previewTranslation.title);
    const summaryChanged = isRealTranslation(article.value.summary, previewTranslation.summary);

    if (titleChanged || summaryChanged) {
      translatedArticle.value = {
        title: previewTranslation.title || article.value.title || "",
        summary: previewTranslation.summary || article.value.summary || "",
        content: article.value.content || "",
      };

      isTranslated.value = false;
    }
  }
}

function hasFullTranslation() {
  return Boolean(
    translatedArticle.value?.title &&
    translatedArticle.value?.summary &&
    translatedArticle.value?.content &&
    isTranslated.value
  );
}

async function translateArticleDetailAutomatically() {
  if (!article.value || translationLoading.value) {
    return;
  }

  translationLoading.value = true;

  try {
    const translator = await createGermanTranslator();

    const translatedTitle = await translateTextToGerman(article.value.title ?? "", translator);
    const translatedSummary = await translateTextToGerman(article.value.summary ?? "", translator);
    const translatedContent = await translateTextToGerman(article.value.content ?? "", translator);

    const titleChanged = isRealTranslation(article.value.title, translatedTitle);
    const summaryChanged = isRealTranslation(article.value.summary, translatedSummary);
    const contentChanged = isRealTranslation(article.value.content, translatedContent);

    if (!titleChanged && !summaryChanged && !contentChanged) {
      return;
    }

    const translation = {
      title: translatedTitle || article.value.title || "",
      summary: translatedSummary || article.value.summary || "",
      content: translatedContent || article.value.content || "",
    };

    translatedArticle.value = translation;
    isTranslated.value = true;

    saveTranslationToStorage(getDetailTranslationStorageKey(article.value), translation);

    saveTranslationToStorage(getPreviewTranslationStorageKey(article.value), {
      title: translation.title,
      summary: translation.summary,
    });
  } catch (err) {
    console.warn("Artikelseite konnte nicht automatisch übersetzt werden:", err);
  } finally {
    translationLoading.value = false;
  }
}

function loadSavedVote() {
  const savedVote = localStorage.getItem(voteStorageKey);

  if (savedVote === "like" || savedVote === "dislike") {
    userVote.value = savedVote;
  }
}

async function likeArticle() {
  actionLoading.value = true;
  error.value = "";

  try {
    await api.post(`/articles/${articleId}/like`);

    userVote.value = "like";
    localStorage.setItem(voteStorageKey, "like");

    await loadArticle();
  } catch (err) {
    console.error(err);

    if (err.response?.status === 401) {
      error.value = "Bitte einloggen, um Artikel zu bewerten.";
    } else {
      error.value = "Like konnte nicht gespeichert werden.";
    }
  } finally {
    actionLoading.value = false;
  }
}

async function dislikeArticle() {
  actionLoading.value = true;
  error.value = "";

  try {
    await api.post(`/articles/${articleId}/dislike`);

    userVote.value = "dislike";
    localStorage.setItem(voteStorageKey, "dislike");

    await loadArticle();
  } catch (err) {
    console.error(err);

    if (err.response?.status === 401) {
      error.value = "Bitte einloggen, um Artikel zu bewerten.";
    } else {
      error.value = "Dislike konnte nicht gespeichert werden.";
    }
  } finally {
    actionLoading.value = false;
  }
}

async function submitComment() {
  const commentText = newComment.value.trim();

  if (!commentText) {
    return;
  }

  commentLoading.value = true;
  error.value = "";

  try {
    await api.post(`/articles/${articleId}/comments`, {
      text: commentText,
    });

    newComment.value = "";
    await loadComments();
  } catch (err) {
    console.error("Kommentar-Fehler:", err.response?.status, err.response?.data || err);

    if (err.response?.status === 401) {
      error.value = "Bitte einloggen, um zu kommentieren.";
    } else if (err.response?.status === 403) {
      error.value = "Du hast keine Berechtigung, Kommentare zu schreiben.";
    } else if (err.response?.status === 400) {
      error.value = "Kommentar konnte nicht gespeichert werden. Das Backend erwartet das Feld text.";
    } else if (err.response?.status === 404) {
      error.value = "Kommentar-Endpunkt oder Artikel wurde nicht gefunden.";
    } else {
      error.value = "Kommentar konnte nicht gespeichert werden.";
    }
  } finally {
    commentLoading.value = false;
  }
}

function formatDate(value) {
  if (!value) {
    return "";
  }

  return new Date(value).toLocaleDateString("de-AT", {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
  });
}

onMounted(async () => {
  await loadArticle();
  await loadComments();
});
</script>

<style scoped>
.article-detail-page {
  min-height: 100vh;
  padding: 2.2rem 1.4rem 4rem;
  background: #f5f5f2;
  color: #111827;
}

.status-box {
  max-width: 960px;
  margin: 0 auto 1rem;
  padding: 1rem 1.25rem;
  border-radius: 16px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
}

.translation-info {
  color: #198754;
  font-weight: 800;
}

.error {
  background: #ffe1e5;
  color: #842029;
  border-color: #f1aeb5;
}

.article-shell {
  max-width: 960px;
  margin: 0 auto;
}

.back-link {
  display: inline-flex;
  margin-bottom: 1rem;
  color: #198754;
  text-decoration: none;
  font-size: 0.85rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.back-link:hover {
  text-decoration: underline;
}

.article-header {
  margin-bottom: 1.2rem;
  padding: 1.6rem;
  border-radius: 24px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
}

.article-meta {
  display: flex;
  flex-wrap: wrap;
  gap: 0.55rem;
  margin-bottom: 0.9rem;
}

.article-meta span {
  display: inline-flex;
  padding: 0.28rem 0.58rem;
  border-radius: 999px;
  background: #f3f4f6;
  color: #4b5563;
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.article-header h1 {
  margin: 0;
  color: #111827;
  font-size: clamp(2rem, 5vw, 4rem);
  line-height: 1.02;
  font-weight: 950;
  letter-spacing: -0.06em;
}

.article-summary {
  margin: 1rem 0 0;
  color: #4b5563;
  font-size: 1.08rem;
  line-height: 1.6;
  font-weight: 650;
}

.translation-note {
  display: inline-flex;
  margin: 1rem 0 0;
  padding: 0.3rem 0.6rem;
  border-radius: 999px;
  background: #e8f5ee;
  color: #198754;
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.article-image-wrap {
  max-height: 330px;
  overflow: hidden;
  border-radius: 22px;
  background: #e5e7eb;
  border: 1px solid #e5e7eb;
}

.article-image {
  width: 100%;
  height: 330px;
  display: block;
  object-fit: cover;
}

.article-content {
  margin-top: 1.2rem;
  padding: 1.6rem;
  border-radius: 24px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
}

.article-content p {
  margin: 0;
  color: #374151;
  font-size: 1.05rem;
  line-height: 1.75;
}

.article-actions {
  margin-top: 1rem;
  padding: 1rem;
  border-radius: 20px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
  display: flex;
  flex-wrap: wrap;
  gap: 0.6rem;
}

.btn,
.source-button {
  border: 1px solid #d1d5db;
  border-radius: 999px;
  padding: 0.62rem 0.95rem;
  font-size: 0.78rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.05em;
  cursor: pointer;
  text-decoration: none;
  transition: 0.15s ease;
}

.vote-button {
  min-width: 138px;
}

.btn-outline-secondary {
  background: #ffffff;
  color: #374151;
}

.btn-outline-secondary:hover {
  border-color: #198754;
  color: #198754;
}

.btn-success {
  background: #198754;
  border-color: #198754;
  color: #ffffff;
}

.btn-danger {
  background: #dc3545;
  border-color: #dc3545;
  color: #ffffff;
}

.btn:disabled {
  opacity: 0.65;
  cursor: not-allowed;
}

.source-button {
  background: #111827;
  border-color: #111827;
  color: #ffffff;
}

.source-button:hover {
  background: #000000;
  border-color: #000000;
}

.comments-section {
  margin-top: 1.2rem;
  padding: 1.6rem;
  border-radius: 24px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
}

.comments-head {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 1rem;
}

.comments-head h2 {
  margin: 0;
  font-size: 1.5rem;
  font-weight: 950;
  letter-spacing: -0.04em;
}

.comments-head span {
  display: inline-flex;
  min-width: 2rem;
  height: 2rem;
  align-items: center;
  justify-content: center;
  border-radius: 999px;
  background: #198754;
  color: #ffffff;
  font-weight: 950;
}

.comment-form {
  display: grid;
  gap: 0.75rem;
  margin-bottom: 1rem;
}

.comment-form textarea {
  width: 100%;
  resize: vertical;
  border: 1px solid #d1d5db;
  border-radius: 18px;
  padding: 0.9rem 1rem;
  font: inherit;
  color: #111827;
  background: #ffffff;
  outline: none;
}

.comment-form textarea:focus {
  border-color: #198754;
}

.comment-form button {
  justify-self: flex-start;
  border: 0;
  border-radius: 999px;
  background: #198754;
  color: #ffffff;
  padding: 0.7rem 1rem;
  font-size: 0.78rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  cursor: pointer;
}

.comment-form button:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.login-hint,
.empty-comments {
  margin-top: 0.8rem;
  padding: 1rem;
  border-radius: 18px;
  background: #f3f4f6;
  color: #4b5563;
  font-weight: 750;
}

.comment-list {
  display: grid;
  gap: 0.75rem;
  margin-top: 1rem;
}

.comment-card {
  padding: 1rem;
  border-radius: 18px;
  background: #f9fafb;
  border: 1px solid #e5e7eb;
}

.comment-meta {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 0.45rem;
  color: #6b7280;
  font-size: 0.78rem;
}

.comment-meta strong {
  color: #111827;
}

.comment-card p {
  margin: 0;
  color: #374151;
  line-height: 1.55;
}

@media (max-width: 760px) {
  .article-detail-page {
    padding: 1.4rem 1rem 3rem;
  }

  .article-header,
  .article-content,
  .comments-section {
    padding: 1.2rem;
    border-radius: 20px;
  }

  .article-header h1 {
    font-size: 2rem;
  }

  .article-summary {
    font-size: 1rem;
  }

  .article-image-wrap {
    max-height: 230px;
    border-radius: 18px;
  }

  .article-image {
    height: 230px;
  }

  .article-actions {
    flex-direction: column;
  }

  .btn,
  .source-button {
    width: 100%;
    text-align: center;
  }

  .comment-meta {
    flex-direction: column;
    gap: 0.2rem;
  }
}
</style>