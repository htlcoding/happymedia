<template>
  <div class="page">
    <section class="hero">
      <p class="eyebrow">Impressum</p>
      <h1>Angaben nach österreichischem Medienrecht</h1>
      <p>
        Diese Seite enthält die grundlegenden Angaben zum Projekt HappyMedia.
      </p>
    </section>

    <section class="content-card">
      <h2>Medieninhaber</h2>
      <p>
        HappyMedia<br />
        Schulprojekt / Diplomarbeitsprojekt
      </p>

      <h2>Kontakt</h2>
      <p>
        E-Mail: bitte hier eure Projekt-E-Mail eintragen
      </p>

      <h2>Verantwortlich für den Inhalt</h2>
      <p>
        Das HappyMedia-Projektteam
      </p>

      <h2>Hinweis</h2>
      <p>
        HappyMedia ist ein technisches Schulprojekt. Die Inhalte stammen aus
        externen RSS-Quellen. Für die Inhalte der verlinkten externen Seiten sind
        die jeweiligen Anbieter verantwortlich.
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