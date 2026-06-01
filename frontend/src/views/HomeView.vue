<template>
  <div class="home-page">
    <div v-if="loading" class="status-box">
      Wird geladen...
    </div>

    <div v-if="error" class="status-box error">
      {{ error }}
    </div>

    <div v-if="translationLoading" class="status-box translation-info">
      Englische Artikel werden automatisch übersetzt...
    </div>

    <section v-if="topArticles.length > 0" class="lead-mosaic">
      <RouterLink :to="`/articles/${mainArticle.id}`" class="lead-card">
        <img
          v-if="mainArticle.imageUrl"
          :src="mainArticle.imageUrl"
          alt="Artikelbild"
        />

        <div v-else class="placeholder" :class="modeClass">
          HappyMedia
        </div>

        <div class="overlay"></div>

        <div class="lead-content">
          <span class="category" :class="modeClass">
            {{ displayCategory(mainArticle) }}
          </span>

          <h2 :title="articleTitle(mainArticle)">
            {{ articleTitle(mainArticle) }}
          </h2>

          <p>
            {{ mainArticle.source }} · Score {{ mainArticle.score }} · {{ formatDate(mainArticle.publishedAt) }}
          </p>
        </div>
      </RouterLink>

      <div class="side-grid">
        <RouterLink
          v-for="article in sideArticles"
          :key="article.id"
          :to="`/articles/${article.id}`"
          class="small-card"
        >
          <img
            v-if="article.imageUrl"
            :src="article.imageUrl"
            alt="Artikelbild"
          />

          <div v-else class="placeholder small" :class="modeClass">
            HappyMedia
          </div>

          <div class="overlay"></div>

          <div class="small-content">
            <span class="category" :class="modeClass">
              {{ displayCategory(article) }}
            </span>

            <h3 :title="articleTitle(article)">
              {{ articleTitle(article) }}
            </h3>

            <p class="small-score">
              {{ article.source }} · Score {{ article.score }}
            </p>
          </div>
        </RouterLink>
      </div>
    </section>

    <section v-if="topArticles.length > 0" class="top-strip">
      <RouterLink
        v-for="(article, index) in topArticles"
        :key="article.id"
        :to="`/articles/${article.id}`"
        class="top-strip-item"
      >
        <span :class="modeClass">
          {{ index + 1 }}
        </span>

        <div>
          <strong :title="articleTitle(article)">
            {{ articleTitle(article) }}
          </strong>

          <small>
            {{ displayCategory(article) }} · {{ article.source }} · Score {{ article.score }}
          </small>
        </div>
      </RouterLink>
    </section>

    <section class="latest-section">
      <div class="section-head">
        <p :class="modeClass">
          {{ sectionTitle }}
        </p>
      </div>

      <div v-if="displayArticles.length > 0" class="article-grid">
        <RouterLink
          v-for="article in displayArticles"
          :key="article.id"
          :to="`/articles/${article.id}`"
          class="article-card"
        >
          <div class="image-wrap">
            <img
              v-if="article.imageUrl"
              :src="article.imageUrl"
              alt="Artikelbild"
            />

            <div v-else class="placeholder small" :class="modeClass">
              HappyMedia
            </div>
          </div>

          <div class="article-body">
            <div class="meta">
              <span>{{ displayCategory(article) }}</span>
              <span>Score {{ article.score }}</span>
            </div>

            <h3 :title="articleTitle(article)">
              {{ articleTitle(article) }}
            </h3>

            <div v-if="displayCategories(article).length > 1" class="category-list">
              <span
                v-for="category in displayCategories(article)"
                :key="category"
              >
                {{ category }}
              </span>
            </div>

            <p>{{ shorten(articleSummary(article), 125) }}</p>

            <small>
              {{ article.source }} · 👍 {{ article.upvotes ?? 0 }} · 👎 {{ article.downvotes ?? 0 }} · 💬 {{ article.commentCount ?? 0 }}
            </small>
          </div>
        </RouterLink>
      </div>

      <div v-else-if="!loading" class="status-box">
        {{ emptyMessage }}
      </div>

      <div v-if="hasMoreArticles" class="load-more-wrap">
        <button
          class="load-more-button"
          :class="modeClass"
          @click="loadMore"
        >
          Mehr anzeigen
        </button>
      </div>
    </section>
  </div>
</template>

<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from "vue";
import { RouterLink, useRoute } from "vue-router";
import api from "../api";
import { isAdmin } from "../auth";
import {
  createGermanTranslator,
  getPreviewTranslationStorageKey,
  isRealTranslation,
  loadTranslationFromStorage,
  looksEnglish,
  saveTranslationToStorage,
  translateTextToGerman,
} from "../translateBrowser";

const route = useRoute();

const articles = ref([]);
const loading = ref(false);
const error = ref("");

const articlesPerLoad = 12;
const visibleCount = ref(articlesPerLoad);

const translatedArticles = ref({});
const translationLoading = ref(false);
const translatingIds = ref(new Set());

const mode = computed(() => {
  if (route.query.mode === "bad") {
    return "bad";
  }

  if (route.query.mode === "neutral") {
    return "neutral";
  }

  return "good";
});

const modeClass = computed(() => {
  return {
    bad: mode.value === "bad",
    neutral: mode.value === "neutral",
  };
});

const sectionTitle = computed(() => {
  if (mode.value === "bad") {
    return "Weitere Bad News";
  }

  if (mode.value === "neutral") {
    return "Weitere Neutral News";
  }

  return "Weitere Good News";
});

const emptyMessage = computed(() => {
  if (mode.value === "bad") {
    return "Noch keine Bad News vorhanden.";
  }

  if (mode.value === "neutral") {
    return "Noch keine neutralen News vorhanden.";
  }

  return "Noch keine Good News vorhanden.";
});

const goodArticles = computed(() => {
  return [...articles.value]
    .filter((article) => Number(article.score ?? 0) > 0)
    .sort((a, b) => Number(b.score ?? 0) - Number(a.score ?? 0));
});

const neutralArticles = computed(() => {
  return [...articles.value]
    .filter((article) => Number(article.score ?? 0) === 0)
    .sort((a, b) => {
      return new Date(b.publishedAt).getTime() - new Date(a.publishedAt).getTime();
    });
});

const badArticles = computed(() => {
  return [...articles.value]
    .filter((article) => Number(article.score ?? 0) < 0)
    .sort((a, b) => Number(a.score ?? 0) - Number(b.score ?? 0));
});

const currentArticles = computed(() => {
  if (mode.value === "bad") {
    return badArticles.value;
  }

  if (mode.value === "neutral") {
    return neutralArticles.value;
  }

  return goodArticles.value;
});

const topArticles = computed(() => {
  return currentArticles.value.slice(0, 5);
});

const mainArticle = computed(() => {
  return topArticles.value[0];
});

const sideArticles = computed(() => {
  return topArticles.value.slice(1, 5);
});

const remainingArticles = computed(() => {
  return currentArticles.value.slice(5);
});

const displayArticles = computed(() => {
  return remainingArticles.value.slice(0, visibleCount.value);
});

const hasMoreArticles = computed(() => {
  return displayArticles.value.length < remainingArticles.value.length;
});

const visibleArticlesForTranslation = computed(() => {
  const visibleMap = new Map();

  for (const article of topArticles.value) {
    visibleMap.set(article.id, article);
  }

  for (const article of displayArticles.value) {
    visibleMap.set(article.id, article);
  }

  return Array.from(visibleMap.values());
});

async function loadArticles() {
  loading.value = true;
  error.value = "";

  try {
    const response = await api.get("/articles");
    articles.value = Array.isArray(response.data) ? response.data : [];
    visibleCount.value = articlesPerLoad;

    await nextTick();
    loadVisibleTranslationsFromStorage();
    translateVisibleArticlesAutomatically();
  } catch (err) {
    console.error(err);

    if (err.response) {
      error.value = `Artikel konnten nicht geladen werden. Status: ${err.response.status}`;
    } else {
      error.value = "Backend nicht erreichbar.";
    }
  } finally {
    loading.value = false;
  }
}

async function importArticles() {
  if (!isAdmin.value) {
    error.value = "Nur admin darf RSS-Import starten.";
    return;
  }

  loading.value = true;
  error.value = "";

  try {
    await api.post("/articles/fetch");
    await loadArticles();
  } catch (err) {
    console.error(err);

    if (err.response?.status === 403) {
      error.value = "Nur admin darf RSS importieren.";
    } else if (err.response?.status === 401) {
      error.value = "Bitte als admin einloggen.";
    } else {
      error.value = "RSS-Import ist fehlgeschlagen.";
    }
  } finally {
    loading.value = false;
  }
}

function loadMore() {
  visibleCount.value += articlesPerLoad;

  nextTick(() => {
    loadVisibleTranslationsFromStorage();
    translateVisibleArticlesAutomatically();
  });
}

function loadVisibleTranslationsFromStorage() {
  const loadedTranslations = { ...translatedArticles.value };

  for (const article of visibleArticlesForTranslation.value) {
    const cached = loadTranslationFromStorage(getPreviewTranslationStorageKey(article));

    if (!cached) {
      continue;
    }

    const titleChanged = isRealTranslation(article.title, cached.title);
    const summaryChanged = isRealTranslation(article.summary, cached.summary);

    if (!titleChanged && !summaryChanged) {
      continue;
    }

    loadedTranslations[article.id] = {
      title: cached.title || article.title || "",
      summary: cached.summary || article.summary || "",
    };
  }

  translatedArticles.value = loadedTranslations;
}

async function translateVisibleArticlesAutomatically() {
  const candidates = visibleArticlesForTranslation.value.filter((article) => {
    if (!article?.id) {
      return false;
    }

    if (!looksEnglish(article)) {
      return false;
    }

    if (translatedArticles.value[article.id]) {
      return false;
    }

    if (translatingIds.value.has(article.id)) {
      return false;
    }

    return true;
  });

  if (candidates.length === 0) {
    return;
  }

  translationLoading.value = true;

  try {
    const translator = await createGermanTranslator();

    for (const article of candidates) {
      await translateArticlePreview(article, translator);
    }
  } catch (err) {
    console.warn("Automatische Übersetzung fehlgeschlagen:", err);
  } finally {
    translationLoading.value = false;
  }
}

async function translateArticlePreview(article, translator) {
  const nextSet = new Set(translatingIds.value);
  nextSet.add(article.id);
  translatingIds.value = nextSet;

  try {
    const cached = loadTranslationFromStorage(getPreviewTranslationStorageKey(article));

    if (cached) {
      const titleChanged = isRealTranslation(article.title, cached.title);
      const summaryChanged = isRealTranslation(article.summary, cached.summary);

      if (titleChanged || summaryChanged) {
        translatedArticles.value = {
          ...translatedArticles.value,
          [article.id]: {
            title: cached.title || article.title || "",
            summary: cached.summary || article.summary || "",
          },
        };

        return;
      }
    }

    const translatedTitle = await translateTextToGerman(article.title ?? "", translator);
    const translatedSummary = await translateTextToGerman(article.summary ?? "", translator);

    const titleChanged = isRealTranslation(article.title, translatedTitle);
    const summaryChanged = isRealTranslation(article.summary, translatedSummary);

    if (!titleChanged && !summaryChanged) {
      return;
    }

    const translation = {
      title: translatedTitle || article.title || "",
      summary: translatedSummary || article.summary || "",
    };

    translatedArticles.value = {
      ...translatedArticles.value,
      [article.id]: translation,
    };

    saveTranslationToStorage(getPreviewTranslationStorageKey(article), translation);
  } catch (err) {
    console.warn("Artikel-Vorschau konnte nicht übersetzt werden:", article.id, err);
  } finally {
    const cleanSet = new Set(translatingIds.value);
    cleanSet.delete(article.id);
    translatingIds.value = cleanSet;
  }
}

function articleTitle(article) {
  if (!article) {
    return "";
  }

  return translatedArticles.value[article.id]?.title || article.title || "";
}

function articleSummary(article) {
  if (!article) {
    return "";
  }

  return translatedArticles.value[article.id]?.summary || article.summary || "";
}

function displayCategory(article) {
  if (!article) {
    return "Allgemein";
  }

  if (article.category && String(article.category).trim()) {
    return String(article.category).trim();
  }

  const categories = displayCategories(article);

  if (categories.length > 0) {
    return categories[0];
  }

  return "Allgemein";
}

function displayCategories(article) {
  const result = [];

  if (article?.category && String(article.category).trim()) {
    result.push(String(article.category).trim());
  }

  const rawCategories = article?.categories;

  if (Array.isArray(rawCategories)) {
    for (const category of rawCategories) {
      addUniqueCategory(result, category);
    }

    return result;
  }

  if (typeof rawCategories === "string" && rawCategories.trim()) {
    const trimmed = rawCategories.trim();

    try {
      const parsed = JSON.parse(trimmed);

      if (Array.isArray(parsed)) {
        for (const category of parsed) {
          addUniqueCategory(result, category);
        }

        return result;
      }
    } catch {
      const parts = trimmed.split(",");

      for (const category of parts) {
        addUniqueCategory(result, category);
      }

      return result;
    }
  }

  return result;
}

function addUniqueCategory(list, category) {
  if (!category) {
    return;
  }

  const cleaned = String(category).trim();

  if (!cleaned || cleaned === "[]") {
    return;
  }

  const alreadyExists = list.some((item) => {
    return item.toLowerCase() === cleaned.toLowerCase();
  });

  if (!alreadyExists) {
    list.push(cleaned);
  }
}

function shorten(text, length) {
  if (!text) {
    return "";
  }

  return text.length > length ? text.substring(0, length) + "..." : text;
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

watch(mode, () => {
  visibleCount.value = articlesPerLoad;

  nextTick(() => {
    loadVisibleTranslationsFromStorage();
    translateVisibleArticlesAutomatically();
  });
});

watch(visibleArticlesForTranslation, () => {
  nextTick(() => {
    loadVisibleTranslationsFromStorage();
    translateVisibleArticlesAutomatically();
  });
});

onMounted(() => {
  loadArticles();

  window.addEventListener("happy-refresh", loadArticles);
  window.addEventListener("happy-import-rss", importArticles);
});

onUnmounted(() => {
  window.removeEventListener("happy-refresh", loadArticles);
  window.removeEventListener("happy-import-rss", importArticles);
});
</script>

<style scoped>
.home-page {
  min-height: 100vh;
  padding-bottom: 4rem;
  background: #f5f5f2;
  color: #111827;
}

* {
  box-sizing: border-box;
}

.lead-mosaic,
.top-strip,
.latest-section,
.status-box {
  max-width: 1440px;
  margin-left: auto;
  margin-right: auto;
}

.status-box {
  margin-top: 1rem;
  margin-bottom: 1rem;
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

.lead-mosaic {
  margin-top: 1rem;
  padding: 0 1.5rem;
  display: grid;
  grid-template-columns: minmax(0, 1.05fr) minmax(0, 1.1fr);
  gap: 0.75rem;
  height: 450px;
  overflow: hidden;
}

.lead-card,
.small-card {
  position: relative;
  overflow: hidden;
  display: block;
  min-width: 0;
  min-height: 0;
  color: #ffffff;
  text-decoration: none;
  background: #1f2937;
  isolation: isolate;
}

.lead-card {
  border-radius: 22px 0 0 22px;
}

.side-grid {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: 0.75rem;
  min-width: 0;
  min-height: 0;
  overflow: hidden;
}

.small-card:nth-child(2) {
  border-radius: 0 22px 0 0;
}

.small-card:nth-child(4) {
  border-radius: 0 0 22px 0;
}

.lead-card img,
.small-card img {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
  transition: 0.25s ease;
}

.lead-card:hover img,
.small-card:hover img,
.article-card:hover img {
  transform: scale(1.025);
}

.overlay {
  position: absolute;
  inset: 0;
  z-index: 1;
  background: linear-gradient(
    to top,
    rgba(0, 0, 0, 0.86),
    rgba(0, 0, 0, 0.28),
    rgba(0, 0, 0, 0.08)
  );
}

.lead-content,
.small-content {
  position: absolute;
  left: 1.15rem;
  right: 1.15rem;
  bottom: 1.05rem;
  z-index: 2;
  max-height: calc(100% - 2rem);
  overflow: hidden;
}

.category {
  display: inline-block;
  max-width: 100%;
  margin-bottom: 0.5rem;
  padding: 0.22rem 0.5rem;
  background: #198754;
  color: #ffffff;
  font-size: 0.68rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.category.bad {
  background: #dc3545;
}

.category.neutral {
  background: #6b7280;
}

.lead-content h2 {
  margin: 0;
  max-width: 100%;
  font-size: clamp(1.75rem, 3.1vw, 3rem);
  line-height: 1.04;
  font-weight: 950;
  letter-spacing: -0.055em;
  overflow: hidden;
  word-break: break-word;
  hyphens: auto;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
}

.lead-content p {
  margin: 0.7rem 0 0;
  color: rgba(255, 255, 255, 0.86);
  font-weight: 750;
  font-size: 0.9rem;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.small-content h3 {
  margin: 0;
  max-width: 100%;
  font-size: clamp(0.95rem, 1.35vw, 1.28rem);
  line-height: 1.1;
  font-weight: 950;
  letter-spacing: -0.035em;
  overflow: hidden;
  word-break: break-word;
  hyphens: auto;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
}

.small-score {
  margin: 0.45rem 0 0;
  color: rgba(255, 255, 255, 0.86);
  font-size: 0.74rem;
  font-weight: 750;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.placeholder {
  width: 100%;
  height: 100%;
  display: grid;
  place-items: center;
  background: linear-gradient(135deg, #145c3a, #198754);
  color: rgba(255, 255, 255, 0.16);
  font-size: 1.65rem;
  font-weight: 950;
  letter-spacing: -0.05em;
}

.placeholder.bad {
  background: linear-gradient(135deg, #dc3545, #7f1d1d);
}

.placeholder.neutral {
  background: linear-gradient(135deg, #6b7280, #374151);
}

.placeholder.small {
  font-size: 1rem;
}

.top-strip {
  margin-top: 1rem;
  padding: 0 1.5rem;
  display: grid;
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 0.75rem;
}

.top-strip-item {
  display: flex;
  gap: 0.75rem;
  min-width: 0;
  min-height: 105px;
  max-height: 125px;
  overflow: hidden;
  padding: 0.9rem;
  border-radius: 18px;
  background: #ffffff;
  color: inherit;
  text-decoration: none;
  border: 1px solid #e5e7eb;
  transition: 0.15s ease;
}

.top-strip-item:hover,
.article-card:hover {
  transform: translateY(-2px);
}

.top-strip-item span {
  flex: 0 0 auto;
  color: #198754;
  font-size: 1.35rem;
  font-weight: 950;
  line-height: 1;
}

.top-strip-item span.bad {
  color: #dc3545;
}

.top-strip-item span.neutral {
  color: #6b7280;
}

.top-strip-item div {
  min-width: 0;
}

.top-strip-item strong {
  display: -webkit-box;
  max-width: 100%;
  overflow: hidden;
  color: #111827;
  font-size: 0.88rem;
  line-height: 1.18;
  font-weight: 900;
  word-break: break-word;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
}

.top-strip-item small {
  display: block;
  margin-top: 0.45rem;
  color: #6b7280;
  font-size: 0.72rem;
  font-weight: 700;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.latest-section {
  margin-top: 2rem;
  padding: 0 1.5rem;
}

.section-head {
  margin-bottom: 1rem;
}

.section-head p {
  margin: 0;
  color: #198754;
  font-size: 0.78rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.14em;
}

.section-head p.bad {
  color: #dc3545;
}

.section-head p.neutral {
  color: #6b7280;
}

.article-grid {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1rem;
  align-items: stretch;
}

.article-card {
  min-width: 0;
  overflow: hidden;
  display: flex;
  flex-direction: column;
  border-radius: 20px;
  background: #ffffff;
  color: inherit;
  text-decoration: none;
  border: 1px solid #e5e7eb;
  transition: 0.15s ease;
}

.image-wrap {
  flex: 0 0 135px;
  height: 135px;
  overflow: hidden;
  background: #e5e7eb;
}

.image-wrap img {
  width: 100%;
  height: 100%;
  display: block;
  object-fit: cover;
  transition: 0.25s ease;
}

.article-body {
  flex: 1;
  min-width: 0;
  padding: 0.9rem;
  display: flex;
  flex-direction: column;
}

.meta {
  display: flex;
  justify-content: space-between;
  gap: 0.75rem;
  margin-bottom: 0.6rem;
  color: #6b7280;
  font-size: 0.68rem;
  font-weight: 850;
  text-transform: uppercase;
  letter-spacing: 0.06em;
}

.meta span {
  min-width: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.article-body h3 {
  margin: 0 0 0.55rem;
  max-width: 100%;
  min-height: 3.55rem;
  color: #111827;
  font-size: 1.05rem;
  line-height: 1.14;
  font-weight: 950;
  letter-spacing: -0.03em;
  overflow: hidden;
  word-break: break-word;
  hyphens: auto;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
}

.category-list {
  display: flex;
  flex-wrap: wrap;
  gap: 0.35rem;
  margin: 0 0 0.65rem;
  max-height: 2.1rem;
  overflow: hidden;
}

.category-list span {
  display: inline-flex;
  border-radius: 999px;
  background: #e8f5ee;
  color: #198754;
  padding: 0.22rem 0.48rem;
  font-size: 0.62rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.04em;
  white-space: nowrap;
}

.article-body p {
  margin: 0 0 0.75rem;
  color: #5b6472;
  font-size: 0.9rem;
  line-height: 1.42;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
}

.article-body small {
  margin-top: auto;
  color: #6b7280;
  font-size: 0.78rem;
  font-weight: 750;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.load-more-wrap {
  margin-top: 1.5rem;
  display: flex;
  justify-content: center;
}

.load-more-button {
  border: 0;
  border-radius: 999px;
  background: #198754;
  color: #ffffff;
  padding: 0.8rem 1.35rem;
  font-size: 0.78rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.08em;
  cursor: pointer;
  transition: 0.15s ease;
}

.load-more-button.bad {
  background: #dc3545;
}

.load-more-button.neutral {
  background: #6b7280;
}

.load-more-button:hover {
  transform: translateY(-2px);
}

@media (max-width: 1100px) {
  .lead-mosaic {
    grid-template-columns: 1fr;
    height: auto;
    overflow: visible;
  }

  .lead-card {
    height: 340px;
    border-radius: 20px 20px 0 0;
  }

  .small-card {
    height: 185px;
  }

  .small-card:nth-child(2),
  .small-card:nth-child(4) {
    border-radius: 0;
  }

  .top-strip {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .article-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .image-wrap {
    flex-basis: 130px;
    height: 130px;
  }
}

@media (max-width: 760px) {
  .side-grid,
  .top-strip,
  .article-grid {
    grid-template-columns: 1fr;
  }

  .lead-card {
    height: 300px;
    border-radius: 20px;
  }

  .small-card {
    height: 175px;
    border-radius: 20px !important;
  }

  .lead-content h2 {
    font-size: 1.75rem;
  }

  .small-content h3 {
    font-size: 1.02rem;
  }

  .image-wrap {
    flex-basis: 125px;
    height: 125px;
  }
}
</style>