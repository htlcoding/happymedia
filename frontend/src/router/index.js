import { createRouter, createWebHistory } from "vue-router";

const router = createRouter({
  history: createWebHistory(),
  routes: [
    {
      path: "/",
      name: "home",
      component: () => import("../views/HomeView.vue"),
    },
    {
      path: "/articles/:id",
      name: "article-detail",
      component: () => import("../views/ArticleDetailView.vue"),
    },
    {
      path: "/statistik",
      name: "statistics",
      component: () => import("../views/StatisticsView.vue"),
    },
    {
      path: "/feeds",
      name: "feeds",
      component: () => import("../views/FeedsView.vue"),
    },
    {
      path: "/login",
      name: "login",
      component: () => import("../views/LoginView.vue"),
    },
    {
      path: "/register",
      name: "register",
      component: () => import("../views/RegisterView.vue"),
    },
    {
      path: "/ueber-uns",
      name: "about",
      component: () => import("../views/AboutView.vue"),
    },
    {
      path: "/impressum",
      name: "impressum",
      component: () => import("../views/ImpressumView.vue"),
    },
    {
      path: "/datenschutz",
      name: "datenschutz",
      component: () => import("../views/DatenschutzView.vue"),
    },
    {
      path: "/:pathMatch(.*)*",
      redirect: "/?mode=good",
    },
  ],
});

export default router;