<template>
  <div class="statistics-page">
    <section class="hero">
      <div class="hero-content">
        <p class="eyebrow">HappyMedia Statistik</p>

        <h1>Wie positiv oder negativ ist die Nachrichtenlage?</h1>

        <p class="intro">
          Eine Übersicht über Quellen, Artikel, Kategorien und die Entwicklung der letzten 7 Tage.
        </p>
      </div>

      <div class="hero-card">
        <span>Durchschnittlicher Score</span>
        <strong :class="scoreClass(statistics?.averageScore ?? 0)">
          {{ formatScore(statistics?.averageScore ?? 0) }}
        </strong>
        <small>über alle Artikel</small>
      </div>
    </section>

    <div v-if="loading" class="status-box">
      Statistik wird geladen...
    </div>

    <div v-if="error" class="status-box error">
      {{ error }}
    </div>

    <template v-if="statistics && !loading">
      <section class="overview-grid">
        <div class="metric-card">
          <span>Artikel</span>
          <strong>{{ statistics.totalArticles ?? 0 }}</strong>
          <small>gesamt importiert</small>
        </div>

        <div class="metric-card positive-card">
          <span>Good News</span>
          <strong>{{ statistics.goodArticles ?? 0 }}</strong>
          <small>positive Artikel</small>
        </div>

        <div class="metric-card neutral-card">
          <span>Neutral</span>
          <strong>{{ statistics.neutralArticles ?? 0 }}</strong>
          <small>neutrale Artikel</small>
        </div>

        <div class="metric-card negative-card">
          <span>Bad News</span>
          <strong>{{ statistics.badArticles ?? 0 }}</strong>
          <small>negative Artikel</small>
        </div>
      </section>

      <section class="score-split-card">
        <div class="section-head">
          <p>Gesamtverteilung</p>
          <h2>Wie ist die Stimmung verteilt?</h2>
        </div>

        <div class="split-bars">
          <div class="split-row">
            <div class="split-label">
              <span>Good News</span>
              <strong>{{ percentage(statistics.goodArticles, statistics.totalArticles) }}%</strong>
            </div>
            <div class="split-track">
              <div
                class="split-fill positive-fill"
                :style="{ width: percentage(statistics.goodArticles, statistics.totalArticles) + '%' }"
              ></div>
            </div>
          </div>

          <div class="split-row">
            <div class="split-label">
              <span>Neutral</span>
              <strong>{{ percentage(statistics.neutralArticles, statistics.totalArticles) }}%</strong>
            </div>
            <div class="split-track">
              <div
                class="split-fill neutral-fill"
                :style="{ width: percentage(statistics.neutralArticles, statistics.totalArticles) + '%' }"
              ></div>
            </div>
          </div>

          <div class="split-row">
            <div class="split-label">
              <span>Bad News</span>
              <strong>{{ percentage(statistics.badArticles, statistics.totalArticles) }}%</strong>
            </div>
            <div class="split-track">
              <div
                class="split-fill negative-fill"
                :style="{ width: percentage(statistics.badArticles, statistics.totalArticles) + '%' }"
              ></div>
            </div>
          </div>
        </div>
      </section>

      <section class="highlight-grid">
        <div class="highlight-card source-card positive-highlight">
          <p>Positivste Quelle</p>
          <h2>{{ statistics.mostPositiveSource?.source ?? "Keine Daten" }}</h2>

          <div class="big-score positive">
            Ø {{ formatScore(statistics.mostPositiveSource?.averageScore ?? 0) }}
          </div>

          <small>
            {{ statistics.mostPositiveSource?.articleCount ?? 0 }} Artikel ·
            Gesamt {{ formatScore(statistics.mostPositiveSource?.totalScore ?? 0) }}
          </small>
        </div>

        <div class="highlight-card source-card negative-highlight">
          <p>Negativste Quelle</p>
          <h2>{{ statistics.mostNegativeSource?.source ?? "Keine Daten" }}</h2>

          <div class="big-score negative">
            Ø {{ formatScore(statistics.mostNegativeSource?.averageScore ?? 0) }}
          </div>

          <small>
            {{ statistics.mostNegativeSource?.articleCount ?? 0 }} Artikel ·
            Gesamt {{ formatScore(statistics.mostNegativeSource?.totalScore ?? 0) }}
          </small>
        </div>

        <RouterLink
          v-if="statistics.bestScoredArticle"
          :to="`/articles/${statistics.bestScoredArticle.id}`"
          class="highlight-card article-highlight positive-highlight"
        >
          <p>Positivster Artikel</p>

          <div class="article-image">
            <img
              v-if="statistics.bestScoredArticle.imageUrl"
              :src="statistics.bestScoredArticle.imageUrl"
              alt="Artikelbild"
            />
            <div v-else class="image-placeholder">
              HappyMedia
            </div>
          </div>

          <div class="article-content">
            <span>Score {{ formatScore(statistics.bestScoredArticle.score) }}</span>
            <h2>{{ statistics.bestScoredArticle.title }}</h2>
            <small>
              {{ statistics.bestScoredArticle.source }} ·
              {{ formatDate(statistics.bestScoredArticle.publishedAt) }}
            </small>
          </div>
        </RouterLink>

        <RouterLink
          v-if="statistics.worstScoredArticle"
          :to="`/articles/${statistics.worstScoredArticle.id}`"
          class="highlight-card article-highlight negative-highlight"
        >
          <p>Negativster Artikel</p>

          <div class="article-image">
            <img
              v-if="statistics.worstScoredArticle.imageUrl"
              :src="statistics.worstScoredArticle.imageUrl"
              alt="Artikelbild"
            />
            <div v-else class="image-placeholder negative-bg">
              HappyMedia
            </div>
          </div>

          <div class="article-content">
            <span>Score {{ formatScore(statistics.worstScoredArticle.score) }}</span>
            <h2>{{ statistics.worstScoredArticle.title }}</h2>
            <small>
              {{ statistics.worstScoredArticle.source }} ·
              {{ formatDate(statistics.worstScoredArticle.publishedAt) }}
            </small>
          </div>
        </RouterLink>
      </section>

      <section class="timeline-card">
        <div class="section-head">
          <p>7-Tage-Entwicklung</p>
          <h2>Durchschnittlicher Score pro Tag</h2>
        </div>

        <div class="chart-wrap">
          <svg viewBox="0 0 700 240" preserveAspectRatio="none" class="line-chart">
            <line x1="0" y1="120" x2="700" y2="120" class="zero-line" />

            <polyline
              v-if="chartPoints"
              :points="chartPoints"
              class="score-line"
              fill="none"
            />

            <circle
              v-for="point in chartPointObjects"
              :key="point.key"
              :cx="point.x"
              :cy="point.y"
              r="5"
              class="score-dot"
              :class="scoreClass(point.value)"
            />
          </svg>

          <div class="day-labels">
            <div
              v-for="day in lastSevenDays"
              :key="day.date"
              class="day-label"
            >
              <strong>{{ formatShortDate(day.date) }}</strong>
              <span :class="scoreClass(day.averageScore)">
                {{ formatScore(day.averageScore) }}
              </span>
            </div>
          </div>
        </div>

        <div class="day-grid">
          <div
            v-for="day in lastSevenDays"
            :key="`card-${day.date}`"
            class="day-card"
          >
            <div>
              <strong>{{ formatShortDate(day.date) }}</strong>
              <span :class="scoreClass(day.scoreChange)">
                {{ formatChange(day.scoreChange) }}
              </span>
            </div>

            <p :class="scoreClass(day.averageScore)">
              Ø {{ formatScore(day.averageScore) }}
            </p>

            <small>
              {{ day.articleCount }} Artikel ·
              {{ day.goodArticles }} gut ·
              {{ day.badArticles }} schlecht
            </small>
          </div>
        </div>
      </section>

      <section class="reaction-grid">
        <RouterLink
          v-if="statistics.mostLikedArticle"
          :to="`/articles/${statistics.mostLikedArticle.id}`"
          class="reaction-card"
        >
          <p>Meist geliketer Artikel</p>
          <span class="reaction-badge positive-bg-soft">
            👍 {{ statistics.mostLikedArticle.upvotes ?? 0 }}
          </span>
          <h3>{{ statistics.mostLikedArticle.title }}</h3>
          <small>
            {{ statistics.mostLikedArticle.source }} ·
            Score {{ formatScore(statistics.mostLikedArticle.score) }}
          </small>
        </RouterLink>

        <RouterLink
          v-if="statistics.mostDislikedArticle"
          :to="`/articles/${statistics.mostDislikedArticle.id}`"
          class="reaction-card"
        >
          <p>Meist disliketer Artikel</p>
          <span class="reaction-badge negative-bg-soft">
            👎 {{ statistics.mostDislikedArticle.downvotes ?? 0 }}
          </span>
          <h3>{{ statistics.mostDislikedArticle.title }}</h3>
          <small>
            {{ statistics.mostDislikedArticle.source }} ·
            Score {{ formatScore(statistics.mostDislikedArticle.score) }}
          </small>
        </RouterLink>

        <RouterLink
          v-if="statistics.mostCommentedArticle"
          :to="`/articles/${statistics.mostCommentedArticle.id}`"
          class="reaction-card"
        >
          <p>Meist kommentierter Artikel</p>
          <span class="reaction-badge neutral-bg-soft">
            💬 {{ statistics.mostCommentedArticle.commentCount ?? 0 }}
          </span>
          <h3>{{ statistics.mostCommentedArticle.title }}</h3>
          <small>
            {{ statistics.mostCommentedArticle.source }} ·
            Score {{ formatScore(statistics.mostCommentedArticle.score) }}
          </small>
        </RouterLink>
      </section>

      <section class="ranking-grid">
        <div class="ranking-card">
          <div class="section-head">
            <p>Quellen</p>
            <h2>Top 10 positivste Quellen</h2>
          </div>

          <div
            v-for="(item, index) in statistics.topPositiveSources ?? []"
            :key="`positive-source-${item.source}`"
            class="ranking-row"
          >
            <div class="rank positive-rank">
              {{ index + 1 }}
            </div>

            <div class="ranking-info">
              <div class="ranking-title">
                <strong>{{ item.source }}</strong>
                <span class="positive">Ø {{ formatScore(item.averageScore) }}</span>
              </div>

              <div class="bar-track">
                <div
                  class="bar-fill positive-fill"
                  :style="{ width: scoreWidth(item.averageScore) }"
                ></div>
              </div>

              <small>
                {{ item.articleCount }} Artikel · Gesamt {{ formatScore(item.totalScore) }} ·
                {{ item.positiveArticles }} positiv · {{ item.negativeArticles }} negativ
              </small>
            </div>
          </div>
        </div>

        <div class="ranking-card">
          <div class="section-head negative-head">
            <p>Quellen</p>
            <h2>Top 10 negativste Quellen</h2>
          </div>

          <div
            v-for="(item, index) in statistics.topNegativeSources ?? []"
            :key="`negative-source-${item.source}`"
            class="ranking-row"
          >
            <div class="rank negative-rank">
              {{ index + 1 }}
            </div>

            <div class="ranking-info">
              <div class="ranking-title">
                <strong>{{ item.source }}</strong>
                <span class="negative">Ø {{ formatScore(item.averageScore) }}</span>
              </div>

              <div class="bar-track">
                <div
                  class="bar-fill negative-fill"
                  :style="{ width: scoreWidth(item.averageScore) }"
                ></div>
              </div>

              <small>
                {{ item.articleCount }} Artikel · Gesamt {{ formatScore(item.totalScore) }} ·
                {{ item.positiveArticles }} positiv · {{ item.negativeArticles }} negativ
              </small>
            </div>
          </div>
        </div>

        <div class="ranking-card">
          <div class="section-head">
            <p>Kategorien</p>
            <h2>Positivste Kategorien</h2>
          </div>

          <div
            v-for="(item, index) in statistics.topPositiveCategories ?? []"
            :key="`positive-category-${item.category}`"
            class="ranking-row"
          >
            <div class="rank positive-rank">
              {{ index + 1 }}
            </div>

            <div class="ranking-info">
              <div class="ranking-title">
                <strong>{{ item.category }}</strong>
                <span class="positive">Ø {{ formatScore(item.averageScore) }}</span>
              </div>

              <div class="bar-track">
                <div
                  class="bar-fill positive-fill"
                  :style="{ width: scoreWidth(item.averageScore) }"
                ></div>
              </div>

              <small>
                {{ item.articleCount }} Artikel · Gesamt {{ formatScore(item.totalScore) }}
              </small>
            </div>
          </div>
        </div>

        <div class="ranking-card">
          <div class="section-head negative-head">
            <p>Kategorien</p>
            <h2>Negativste Kategorien</h2>
          </div>

          <div
            v-for="(item, index) in statistics.topNegativeCategories ?? []"
            :key="`negative-category-${item.category}`"
            class="ranking-row"
          >
            <div class="rank negative-rank">
              {{ index + 1 }}
            </div>

            <div class="ranking-info">
              <div class="ranking-title">
                <strong>{{ item.category }}</strong>
                <span class="negative">Ø {{ formatScore(item.averageScore) }}</span>
              </div>

              <div class="bar-track">
                <div
                  class="bar-fill negative-fill"
                  :style="{ width: scoreWidth(item.averageScore) }"
                ></div>
              </div>

              <small>
                {{ item.articleCount }} Artikel · Gesamt {{ formatScore(item.totalScore) }}
              </small>
            </div>
          </div>
        </div>
      </section>
    </template>
  </div>
</template>

<script setup>
import { computed, onMounted, ref } from "vue";
import { RouterLink } from "vue-router";
import api from "../api";

const statistics = ref(null);
const loading = ref(false);
const error = ref("");

const lastSevenDays = computed(() => {
  return Array.isArray(statistics.value?.lastSevenDays)
    ? statistics.value.lastSevenDays
    : [];
});

const chartPointObjects = computed(() => {
  const days = lastSevenDays.value;

  if (days.length === 0) {
    return [];
  }

  const width = 700;
  const height = 240;
  const minScore = -100;
  const maxScore = 100;

  return days.map((day, index) => {
    const x = days.length === 1 ? width / 2 : (index / (days.length - 1)) * width;
    const value = Number(day.averageScore ?? 0);
    const normalized = (value - minScore) / (maxScore - minScore);
    const y = height - normalized * height;

    return {
      key: day.date,
      x,
      y,
      value,
    };
  });
});

const chartPoints = computed(() => {
  return chartPointObjects.value
    .map((point) => `${point.x},${point.y}`)
    .join(" ");
});

async function loadStatistics() {
  loading.value = true;
  error.value = "";

  try {
    const response = await api.get("/statistics");
    statistics.value = response.data;
  } catch (err) {
    console.error("Statistik Fehler:", err);

    if (err.response) {
      error.value = `Statistik konnte nicht geladen werden. Status: ${err.response.status}`;
    } else if (err.request) {
      error.value = "Statistik-Endpoint nicht erreichbar. Prüfe https://localhost:7045/api/statistics";
    } else {
      error.value = `Fehler: ${err.message}`;
    }
  } finally {
    loading.value = false;
  }
}

function formatScore(value) {
  const number = Number(value ?? 0);

  if (number > 0) {
    return `+${Math.round(number)}`;
  }

  return `${Math.round(number)}`;
}

function formatChange(value) {
  const number = Number(value ?? 0);

  if (number > 0) {
    return `+${Math.round(number)} zum Vortag`;
  }

  if (number < 0) {
    return `${Math.round(number)} zum Vortag`;
  }

  return "±0 zum Vortag";
}

function scoreClass(value) {
  const number = Number(value ?? 0);

  if (number > 0) {
    return "positive";
  }

  if (number < 0) {
    return "negative";
  }

  return "neutral";
}

function scoreWidth(value) {
  const number = Math.abs(Number(value ?? 0));
  const width = Math.min(100, Math.max(6, number));

  return `${width}%`;
}

function percentage(value, total) {
  const number = Number(value ?? 0);
  const all = Number(total ?? 0);

  if (all <= 0) {
    return 0;
  }

  return Math.round((number / all) * 100);
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

function formatShortDate(value) {
  if (!value) {
    return "";
  }

  return new Date(value).toLocaleDateString("de-AT", {
    day: "2-digit",
    month: "2-digit",
  });
}

onMounted(() => {
  loadStatistics();
});
</script>

<style scoped>
.statistics-page {
  min-height: 100vh;
  padding: 1rem 1.5rem 4rem;
  background:
    radial-gradient(circle at top left, rgba(25, 135, 84, 0.08), transparent 28rem),
    radial-gradient(circle at top right, rgba(220, 53, 69, 0.06), transparent 24rem),
    #f5f5f2;
  color: #111827;
}

* {
  box-sizing: border-box;
}

.hero,
.overview-grid,
.score-split-card,
.highlight-grid,
.timeline-card,
.reaction-grid,
.ranking-grid,
.status-box {
  max-width: 1440px;
  margin-left: auto;
  margin-right: auto;
}

.hero {
  margin-top: 1rem;
  padding: 2rem;
  border-radius: 30px;
  background:
    linear-gradient(135deg, rgba(17, 24, 39, 0.96), rgba(20, 92, 58, 0.95)),
    radial-gradient(circle at top right, rgba(255, 255, 255, 0.18), transparent 22rem);
  color: #ffffff;
  display: grid;
  grid-template-columns: minmax(0, 1fr) 320px;
  gap: 2rem;
  align-items: end;
}

.eyebrow {
  margin: 0 0 0.8rem;
  color: rgba(255, 255, 255, 0.72);
  font-size: 0.78rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.16em;
}

.hero h1 {
  max-width: 900px;
  margin: 0;
  font-size: clamp(2.25rem, 4.6vw, 4.8rem);
  line-height: 0.96;
  font-weight: 950;
  letter-spacing: -0.07em;
}

.intro {
  max-width: 760px;
  margin: 1rem 0 0;
  color: rgba(255, 255, 255, 0.86);
  font-size: 1rem;
  line-height: 1.55;
  font-weight: 650;
}

.hero-card {
  padding: 1.25rem;
  border-radius: 24px;
  background: rgba(255, 255, 255, 0.13);
  border: 1px solid rgba(255, 255, 255, 0.22);
  backdrop-filter: blur(12px);
}

.hero-card span,
.hero-card small {
  display: block;
  color: rgba(255, 255, 255, 0.72);
  font-size: 0.72rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.1em;
}

.hero-card strong {
  display: block;
  margin: 0.7rem 0;
  font-size: 3.2rem;
  line-height: 1;
  font-weight: 950;
  letter-spacing: -0.08em;
}

.status-box {
  margin-top: 1rem;
  padding: 1rem 1.25rem;
  border-radius: 18px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
  font-weight: 850;
}

.error {
  background: #ffe1e5;
  color: #842029;
  border-color: #f1aeb5;
}

.overview-grid {
  margin-top: 1rem;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 0.85rem;
}

.metric-card,
.score-split-card,
.highlight-card,
.timeline-card,
.reaction-card,
.ranking-card {
  background: #ffffff;
  border: 1px solid #e5e7eb;
  box-shadow: 0 18px 45px rgba(15, 23, 42, 0.045);
}

.metric-card {
  min-height: 128px;
  padding: 1rem;
  border-radius: 24px;
  position: relative;
  overflow: hidden;
}

.metric-card::after {
  content: "";
  position: absolute;
  right: -2.4rem;
  bottom: -2.4rem;
  width: 7rem;
  height: 7rem;
  border-radius: 50%;
  background: #f3f4f6;
}

.metric-card span {
  display: block;
  color: #6b7280;
  font-size: 0.72rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.1em;
}

.metric-card strong {
  display: block;
  position: relative;
  z-index: 1;
  margin-top: 1.1rem;
  color: #111827;
  font-size: 2.35rem;
  line-height: 1;
  font-weight: 950;
  letter-spacing: -0.06em;
}

.metric-card small {
  display: block;
  position: relative;
  z-index: 1;
  margin-top: 0.5rem;
  color: #6b7280;
  font-size: 0.76rem;
  font-weight: 800;
}

.positive-card strong,
.positive {
  color: #198754 !important;
}

.neutral-card strong,
.neutral {
  color: #6b7280 !important;
}

.negative-card strong,
.negative {
  color: #dc3545 !important;
}

.score-split-card,
.timeline-card {
  margin-top: 1rem;
  padding: 1.35rem;
  border-radius: 26px;
}

.section-head {
  margin-bottom: 1.1rem;
}

.section-head p {
  margin: 0 0 0.35rem;
  color: #198754;
  font-size: 0.72rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.12em;
}

.negative-head p {
  color: #dc3545;
}

.section-head h2 {
  margin: 0;
  color: #111827;
  font-size: 1.55rem;
  line-height: 1.06;
  font-weight: 950;
  letter-spacing: -0.045em;
}

.split-bars {
  display: grid;
  gap: 0.9rem;
}

.split-row {
  display: grid;
  gap: 0.45rem;
}

.split-label {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  color: #111827;
  font-size: 0.86rem;
  font-weight: 900;
}

.split-track,
.bar-track {
  height: 12px;
  overflow: hidden;
  border-radius: 999px;
  background: #e5e7eb;
}

.split-fill,
.bar-fill {
  height: 100%;
  min-width: 6px;
  border-radius: 999px;
}

.positive-fill {
  background: #198754;
}

.neutral-fill {
  background: #6b7280;
}

.negative-fill {
  background: #dc3545;
}

.highlight-grid {
  margin-top: 1rem;
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 1rem;
}

.highlight-card {
  overflow: hidden;
  border-radius: 26px;
  color: inherit;
  text-decoration: none;
}

.source-card {
  padding: 1rem;
}

.highlight-card > p {
  margin: 0 0 0.85rem;
  color: #198754;
  font-size: 0.72rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.11em;
}

.negative-highlight > p {
  color: #dc3545;
}

.source-card h2 {
  min-height: 4.2rem;
  margin: 0;
  color: #111827;
  font-size: 1.55rem;
  line-height: 1.04;
  font-weight: 950;
  letter-spacing: -0.055em;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
}

.big-score {
  margin-top: 1rem;
  font-size: 2.4rem;
  line-height: 1;
  font-weight: 950;
  letter-spacing: -0.07em;
}

.source-card small {
  display: block;
  margin-top: 0.65rem;
  color: #6b7280;
  font-size: 0.78rem;
  line-height: 1.45;
  font-weight: 800;
}

.article-image {
  height: 150px;
  overflow: hidden;
  background: #e5e7eb;
}

.article-image img,
.image-placeholder {
  width: 100%;
  height: 100%;
  object-fit: cover;
}

.image-placeholder {
  display: grid;
  place-items: center;
  background: linear-gradient(135deg, #145c3a, #198754);
  color: rgba(255, 255, 255, 0.18);
  font-size: 1.2rem;
  font-weight: 950;
}

.negative-bg {
  background: linear-gradient(135deg, #7f1d1d, #dc3545);
}

.article-content {
  padding: 1rem;
}

.article-content span,
.reaction-badge {
  display: inline-flex;
  margin-bottom: 0.6rem;
  border-radius: 999px;
  padding: 0.28rem 0.6rem;
  background: #e8f5ee;
  color: #198754;
  font-size: 0.7rem;
  font-weight: 950;
}

.negative-highlight .article-content span {
  background: #ffe1e5;
  color: #dc3545;
}

.article-content h2 {
  min-height: 3.4rem;
  margin: 0 0 0.65rem;
  color: #111827;
  font-size: 1.05rem;
  line-height: 1.14;
  font-weight: 950;
  letter-spacing: -0.035em;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 3;
  -webkit-box-orient: vertical;
}

.article-content small {
  color: #6b7280;
  font-size: 0.74rem;
  line-height: 1.35;
  font-weight: 750;
}

.chart-wrap {
  padding: 1rem;
  border-radius: 22px;
  background: #f7f7f4;
  border: 1px solid #e5e7eb;
}

.line-chart {
  width: 100%;
  height: 240px;
  display: block;
}

.zero-line {
  stroke: #cbd5e1;
  stroke-width: 1;
  stroke-dasharray: 8 8;
}

.score-line {
  stroke: #198754;
  stroke-width: 5;
  stroke-linecap: round;
  stroke-linejoin: round;
}

.score-dot {
  fill: #6b7280;
  stroke: #ffffff;
  stroke-width: 4;
}

.score-dot.positive {
  fill: #198754;
}

.score-dot.negative {
  fill: #dc3545;
}

.day-labels {
  margin-top: 0.8rem;
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  gap: 0.5rem;
}

.day-label {
  padding: 0.7rem;
  border-radius: 16px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
}

.day-label strong,
.day-label span {
  display: block;
  font-size: 0.78rem;
  font-weight: 950;
}

.day-label span {
  margin-top: 0.25rem;
}

.day-grid {
  margin-top: 1rem;
  display: grid;
  grid-template-columns: repeat(7, minmax(0, 1fr));
  gap: 0.75rem;
}

.day-card {
  padding: 0.85rem;
  border-radius: 18px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
}

.day-card div {
  display: flex;
  justify-content: space-between;
  gap: 0.5rem;
}

.day-card div strong {
  font-size: 0.86rem;
  font-weight: 950;
}

.day-card div span {
  font-size: 0.64rem;
  font-weight: 900;
  text-align: right;
}

.day-card p {
  margin: 0.8rem 0 0.45rem;
  font-size: 1.55rem;
  line-height: 1;
  font-weight: 950;
  letter-spacing: -0.05em;
}

.day-card small {
  color: #6b7280;
  font-size: 0.7rem;
  line-height: 1.35;
  font-weight: 800;
}

.reaction-grid {
  margin-top: 1rem;
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 1rem;
}

.reaction-card {
  padding: 1rem;
  border-radius: 24px;
  color: inherit;
  text-decoration: none;
}

.reaction-card p {
  margin: 0 0 0.85rem;
  color: #6b7280;
  font-size: 0.72rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.11em;
}

.positive-bg-soft {
  background: #e8f5ee;
  color: #198754;
}

.negative-bg-soft {
  background: #ffe1e5;
  color: #dc3545;
}

.neutral-bg-soft {
  background: #f3f4f6;
  color: #6b7280;
}

.reaction-card h3 {
  margin: 0 0 0.65rem;
  color: #111827;
  font-size: 1.1rem;
  line-height: 1.14;
  font-weight: 950;
  letter-spacing: -0.035em;
  overflow: hidden;
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
}

.reaction-card small {
  color: #6b7280;
  font-size: 0.75rem;
  font-weight: 800;
}

.ranking-grid {
  margin-top: 1rem;
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 1rem;
}

.ranking-card {
  padding: 1.25rem;
  border-radius: 26px;
}

.ranking-row {
  display: flex;
  gap: 0.85rem;
  padding: 0.9rem;
  border-radius: 18px;
  background: #f7f7f4;
  border: 1px solid #e5e7eb;
}

.ranking-row + .ranking-row {
  margin-top: 0.75rem;
}

.rank {
  flex: 0 0 34px;
  width: 34px;
  height: 34px;
  display: grid;
  place-items: center;
  border-radius: 999px;
  color: #ffffff;
  font-size: 0.85rem;
  font-weight: 950;
}

.positive-rank {
  background: #198754;
}

.negative-rank {
  background: #dc3545;
}

.ranking-info {
  flex: 1;
  min-width: 0;
}

.ranking-title {
  display: flex;
  justify-content: space-between;
  gap: 1rem;
  margin-bottom: 0.55rem;
}

.ranking-title strong {
  min-width: 0;
  color: #111827;
  font-size: 0.95rem;
  font-weight: 950;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.ranking-title span {
  flex: 0 0 auto;
  font-size: 0.82rem;
  font-weight: 950;
}

.ranking-info small {
  display: block;
  margin-top: 0.55rem;
  color: #6b7280;
  font-size: 0.74rem;
  line-height: 1.35;
  font-weight: 800;
}

@media (max-width: 1250px) {
  .highlight-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .day-grid,
  .day-labels {
    grid-template-columns: repeat(4, minmax(0, 1fr));
  }
}

@media (max-width: 1000px) {
  .hero {
    grid-template-columns: 1fr;
  }

  .overview-grid,
  .ranking-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }

  .reaction-grid {
    grid-template-columns: 1fr;
  }
}

@media (max-width: 760px) {
  .statistics-page {
    padding-left: 1rem;
    padding-right: 1rem;
  }

  .hero {
    padding: 1.35rem;
  }

  .overview-grid,
  .highlight-grid,
  .ranking-grid,
  .day-grid,
  .day-labels {
    grid-template-columns: 1fr;
  }

  .ranking-title {
    flex-direction: column;
    gap: 0.25rem;
  }

  .ranking-title strong {
    white-space: normal;
  }
}
</style>