<template>
  <div class="app-shell">
    <header class="site-header">
      <div class="nav-inner">
        <RouterLink class="brand" to="/?mode=good">
          Happy<span>Media</span>
        </RouterLink>

        <nav class="nav-links">
          <RouterLink
            to="/?mode=good"
            class="nav-pill"
            :class="{ activeGood: isGoodActive }"
          >
            Good News
          </RouterLink>

          <RouterLink
            to="/?mode=neutral"
            class="nav-pill"
            :class="{ activeNeutral: isNeutralActive }"
          >
            Neutral News
          </RouterLink>

          <RouterLink
            to="/?mode=bad"
            class="nav-pill"
            :class="{ activeBad: isBadActive }"
          >
            Bad News
          </RouterLink>

          <RouterLink
            to="/statistik"
            class="nav-pill"
            :class="{ activeGood: isStatisticsActive }"
          >
            Statistik
          </RouterLink>

          <button
            v-if="isAdmin"
            class="nav-pill dark"
            @click="importRss"
          >
            RSS importieren
          </button>

          <RouterLink
            v-if="isAdmin"
            to="/feeds"
            class="nav-text"
            :class="{ activeText: isFeedsActive }"
          >
            Quellen
          </RouterLink>

          <RouterLink
            v-if="!isLoggedIn"
            to="/login"
            class="nav-pill"
            :class="{ activeGood: isLoginActive }"
          >
            Login
          </RouterLink>

          <button
            v-if="isLoggedIn"
            class="nav-pill dark"
            @click="logout"
          >
            Logout
          </button>
        </nav>
      </div>
    </header>

    <main class="main-content">
      <RouterView />
    </main>

    <footer class="site-footer">
      <div class="footer-inner">
        <div class="footer-brand-block">
          <RouterLink class="footer-brand" to="/?mode=good">
            Happy<span>Media</span>
          </RouterLink>

          <p>
            Nachrichten übersichtlich, verständlich und nach Stimmung sortiert:
            Good News, Neutral News und Bad News.
          </p>
        </div>

        <nav class="footer-links">
          <RouterLink to="/statistik">
            Statistik
          </RouterLink>

          <RouterLink to="/ueber-uns">
            Über uns
          </RouterLink>

          <RouterLink to="/impressum">
            Impressum
          </RouterLink>

          <RouterLink to="/datenschutz">
            Datenschutz
          </RouterLink>
        </nav>
      </div>

      <div class="footer-bottom">
        © {{ currentYear }} HappyMedia. Alle Rechte vorbehalten.
      </div>
    </footer>
  </div>
</template>

<script setup>
import { computed, nextTick } from "vue";
import { RouterLink, RouterView, useRoute, useRouter } from "vue-router";
import { clearAuth, isAdmin, isLoggedIn } from "./auth";

const route = useRoute();
const router = useRouter();

const currentYear = new Date().getFullYear();

const isGoodActive = computed(() => {
  return route.path === "/" && route.query.mode !== "bad" && route.query.mode !== "neutral";
});

const isNeutralActive = computed(() => {
  return route.path === "/" && route.query.mode === "neutral";
});

const isBadActive = computed(() => {
  return route.path === "/" && route.query.mode === "bad";
});

const isStatisticsActive = computed(() => {
  return route.path === "/statistik";
});

const isFeedsActive = computed(() => {
  return route.path === "/feeds";
});

const isLoginActive = computed(() => {
  return route.path === "/login";
});

async function importRss() {
  if (route.path !== "/") {
    await router.push("/?mode=good");
    await nextTick();
  }

  window.dispatchEvent(new Event("happy-import-rss"));
}

function logout() {
  clearAuth();
  router.push("/login");
}
</script>

<style scoped>
.app-shell {
  min-height: 100vh;
  background: #f5f5f2;
  display: flex;
  flex-direction: column;
}

.site-header {
  position: sticky;
  top: 0;
  z-index: 50;
  background: rgba(255, 255, 255, 0.94);
  border-bottom: 1px solid #e5e7eb;
  backdrop-filter: blur(14px);
}

.nav-inner {
  max-width: 1440px;
  margin: 0 auto;
  padding: 0.75rem 1.4rem;
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1.2rem;
}

.brand {
  flex: 0 0 auto;
  color: #111827;
  text-decoration: none;
  font-size: 1.75rem;
  font-weight: 950;
  letter-spacing: -0.06em;
  line-height: 1;
}

.brand span {
  color: #198754;
}

.nav-links {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.45rem;
  flex-wrap: wrap;
}

.nav-pill,
.nav-text {
  border: 1px solid transparent;
  background: transparent;
  color: #111827;
  border-radius: 999px;
  padding: 0.42rem 0.68rem;
  font-size: 0.72rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.045em;
  text-decoration: none;
  cursor: pointer;
  line-height: 1.1;
  transition: 0.15s ease;
  white-space: nowrap;
}

.nav-pill {
  border-color: #d1d5db;
  background: #ffffff;
}

.nav-pill:hover,
.nav-text:hover {
  color: #198754;
  border-color: #198754;
}

.nav-pill.activeGood {
  background: #198754;
  border-color: #198754;
  color: #ffffff !important;
}

.nav-pill.activeNeutral {
  background: #6b7280;
  border-color: #6b7280;
  color: #ffffff !important;
}

.nav-pill.activeBad {
  background: #dc3545;
  border-color: #dc3545;
  color: #ffffff !important;
}

.nav-pill.dark {
  background: #111827;
  border-color: #111827;
  color: #ffffff !important;
}

.nav-pill.dark:hover {
  background: #000000;
  border-color: #000000;
}

.nav-text.activeText {
  color: #198754;
  border-color: #198754;
}

.main-content {
  flex: 1;
}

.site-footer {
  margin-top: auto;
  background: #111827;
  color: #ffffff;
}

.footer-inner {
  max-width: 1440px;
  margin: 0 auto;
  padding: 2rem 1.4rem;
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 2rem;
}

.footer-brand-block {
  max-width: 520px;
}

.footer-brand {
  display: inline-block;
  margin-bottom: 0.7rem;
  color: #ffffff;
  text-decoration: none;
  font-size: 1.65rem;
  font-weight: 950;
  letter-spacing: -0.06em;
  line-height: 1;
}

.footer-brand span {
  color: #5dbb84;
}

.footer-brand-block p {
  margin: 0;
  color: #d1d5db;
  font-size: 0.92rem;
  line-height: 1.55;
}

.footer-links {
  display: flex;
  align-items: center;
  justify-content: flex-end;
  gap: 0.6rem;
  flex-wrap: wrap;
}

.footer-links a {
  color: #ffffff;
  text-decoration: none;
  border: 1px solid #374151;
  border-radius: 999px;
  padding: 0.48rem 0.75rem;
  font-size: 0.75rem;
  font-weight: 900;
  text-transform: uppercase;
  letter-spacing: 0.06em;
  transition: 0.15s ease;
  white-space: nowrap;
}

.footer-links a:hover {
  background: #198754;
  border-color: #198754;
  color: #ffffff;
}

.footer-bottom {
  border-top: 1px solid #1f2937;
  padding: 0.9rem 1.4rem;
  color: #9ca3af;
  text-align: center;
  font-size: 0.78rem;
  font-weight: 700;
}

@media (max-width: 900px) {
  .nav-inner {
    align-items: flex-start;
    flex-direction: column;
  }

  .nav-links {
    justify-content: flex-start;
  }
}

@media (max-width: 760px) {
  .nav-inner {
    padding: 0.75rem 1rem;
  }

  .brand {
    font-size: 1.55rem;
  }

  .nav-links {
    gap: 0.35rem;
  }

  .nav-pill,
  .nav-text {
    font-size: 0.68rem;
    padding: 0.38rem 0.58rem;
  }

  .footer-inner {
    flex-direction: column;
    padding: 1.6rem 1.2rem;
  }

  .footer-links {
    align-items: flex-start;
    justify-content: flex-start;
  }

  .footer-links a {
    font-size: 0.7rem;
    padding: 0.42rem 0.65rem;
  }
}
</style>