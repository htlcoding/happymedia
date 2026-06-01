<template>
  <div class="page">
    <section class="hero">
      <p class="eyebrow">Über uns</p>
      <h1>HappyMedia sortiert Nachrichten nach Stimmung und Relevanz.</h1>
      <p>
        HappyMedia sammelt Nachrichten aus verschiedenen Quellen und bewertet sie nach
        positiver, neutraler oder negativer Wirkung. So können Nutzerinnen und Nutzer
        schnell sehen, welche Themen Hoffnung machen, welche neutral sind und welche
        eher negativ bewertet werden.
      </p>
    </section>

    <section class="content-card">
      <h2>Was ist HappyMedia?</h2>
      <p>
        HappyMedia ist ein Nachrichtenprojekt, das Artikel aus RSS-Quellen sammelt,
        analysiert und übersichtlich darstellt. Die Artikel werden mit Scores,
        Kategorien und Reaktionen der Nutzerinnen und Nutzer angezeigt.
      </p>

      <h2>Was bedeuten Good News, Neutral News und Bad News?</h2>
      <p>
        Good News sind Artikel mit positiver Bewertung. Neutral News sind Artikel ohne
        klar positive oder negative Einordnung. Bad News sind Artikel mit negativer
        Bewertung.
      </p>

      <h2>Warum gibt es eine Statistik-Seite?</h2>
      <p>
        Die Statistik-Seite zeigt, welche Quellen besonders positiv abschneiden,
        welche Kategorien viele Punkte sammeln und welche Artikel am meisten geliket
        oder disliket wurden.
      </p>
    </section>
  </div>
</template>

<script setup>
</script>

<style scoped>
.page {
  min-height: 100vh;
  padding: 1rem 1.5rem 4rem;
  background: #f5f5f2;
  color: #111827;
}

.hero,
.content-card {
  max-width: 1100px;
  margin-left: auto;
  margin-right: auto;
}

.hero {
  margin-top: 1rem;
  padding: 2rem;
  border-radius: 24px;
  background: linear-gradient(135deg, #145c3a, #198754);
  color: #ffffff;
}

.eyebrow {
  margin: 0 0 0.75rem;
  color: rgba(255, 255, 255, 0.78);
  font-size: 0.78rem;
  font-weight: 950;
  text-transform: uppercase;
  letter-spacing: 0.16em;
}

.hero h1 {
  max-width: 850px;
  margin: 0;
  font-size: clamp(2rem, 4vw, 4rem);
  line-height: 0.98;
  font-weight: 950;
  letter-spacing: -0.065em;
}

.hero p {
  max-width: 760px;
  margin: 1rem 0 0;
  color: rgba(255, 255, 255, 0.88);
  font-size: 1rem;
  line-height: 1.55;
  font-weight: 650;
}

.content-card {
  margin-top: 1rem;
  padding: 1.5rem;
  border-radius: 22px;
  background: #ffffff;
  border: 1px solid #e5e7eb;
}

.content-card h2 {
  margin: 1.2rem 0 0.45rem;
  color: #111827;
  font-size: 1.35rem;
  line-height: 1.1;
  font-weight: 950;
  letter-spacing: -0.035em;
}

.content-card h2:first-child {
  margin-top: 0;
}

.content-card p {
  margin: 0;
  color: #4b5563;
  font-size: 0.98rem;
  line-height: 1.65;
  font-weight: 600;
}

@media (max-width: 760px) {
  .page {
    padding-left: 1rem;
    padding-right: 1rem;
  }

  .hero {
    padding: 1.35rem;
  }

  .content-card {
    padding: 1.2rem;
  }
}
</style>