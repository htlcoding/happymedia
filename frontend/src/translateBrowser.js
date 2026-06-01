export function looksEnglish(articleOrText) {
  let text = "";
  let source = "";

  if (typeof articleOrText === "string") {
    text = articleOrText;
  } else {
    source = String(articleOrText?.source ?? "").toLowerCase();
    text = `${articleOrText?.title ?? ""} ${articleOrText?.summary ?? ""} ${articleOrText?.content ?? ""}`;
  }

  const normalizedText = String(text).toLowerCase();

  if (!normalizedText.trim()) {
    return false;
  }

  if (/[äöüß]/i.test(normalizedText)) {
    return false;
  }

  const englishSources = [
    "bbc",
    "guardian",
    "sky",
    "npr",
    "cnn",
    "reuters",
    "ap",
    "the verge",
    "verge",
    "wired",
    "techcrunch",
    "ars technica",
    "sciencedaily",
    "le monde english",
    "cbc",
    "new york times",
    "washington post",
    "politico",
    "axios",
    "vox",
    "al jazeera english",
    "nature",
    "who news",
    "un news",
    "nasa"
  ];

  if (englishSources.some((word) => source.includes(word))) {
    return true;
  }

  const paddedText = ` ${normalizedText} `;

  const englishWords = [
    " the ",
    " and ",
    " is ",
    " are ",
    " was ",
    " were ",
    " with ",
    " for ",
    " from ",
    " after ",
    " before ",
    " over ",
    " under ",
    " world ",
    " government ",
    " people ",
    " police ",
    " health ",
    " science ",
    " business ",
    " technology ",
    " climate ",
    " president ",
    " minister ",
    " court ",
    " election ",
    " economy ",
    " market ",
    " company ",
    " researchers ",
    " study ",
    " says ",
    " said "
  ];

  const hits = englishWords.filter((word) => paddedText.includes(word)).length;

  return hits >= 1;
}

export function getPreviewTranslationStorageKey(article) {
  const updatedPart = article?.publishedAt || article?.createdAt || "";
  return `happypedia-auto-translation-preview-v2-${article?.id}-${updatedPart}`;
}

export function getDetailTranslationStorageKey(article) {
  const updatedPart = article?.publishedAt || article?.createdAt || "";
  return `happypedia-auto-translation-detail-v2-${article?.id}-${updatedPart}`;
}

export function loadTranslationFromStorage(key) {
  try {
    const raw = localStorage.getItem(key);

    if (!raw) {
      return null;
    }

    const parsed = JSON.parse(raw);

    if (!parsed?.title && !parsed?.summary && !parsed?.content) {
      return null;
    }

    return parsed;
  } catch {
    return null;
  }
}

export function saveTranslationToStorage(key, translation) {
  try {
    localStorage.setItem(key, JSON.stringify(translation));
  } catch {
    // localStorage kann blockiert sein
  }
}

export async function createGermanTranslator() {
  return {
    async translate(text) {
      return await translateWithGoogleFrontend(text);
    }
  };
}

export async function translateTextToGerman(text, translator) {
  if (!text || !String(text).trim()) {
    return text;
  }

  if (!translator || typeof translator.translate !== "function") {
    return text;
  }

  try {
    const translated = await translator.translate(String(text));
    return translated || text;
  } catch (error) {
    console.warn("Text konnte nicht übersetzt werden:", error);
    return text;
  }
}

export function isRealTranslation(original, translated) {
  const cleanOriginal = String(original ?? "").trim();
  const cleanTranslated = String(translated ?? "").trim();

  if (!cleanOriginal || !cleanTranslated) {
    return false;
  }

  return cleanOriginal !== cleanTranslated;
}

async function translateWithGoogleFrontend(text) {
  const cleanText = String(text ?? "").trim();

  if (!cleanText) {
    return text;
  }

  const chunks = splitTextIntoChunks(cleanText, 1200);
  const translatedChunks = [];

  for (const chunk of chunks) {
    const translatedChunk = await translateChunk(chunk);
    translatedChunks.push(translatedChunk);
  }

  return translatedChunks.join(" ").trim() || text;
}

async function translateChunk(text) {
  const url =
    "https://translate.googleapis.com/translate_a/single" +
    "?client=gtx" +
    "&sl=en" +
    "&tl=de" +
    "&dt=t" +
    "&q=" +
    encodeURIComponent(text);

  const response = await fetch(url);

  if (!response.ok) {
    throw new Error(`Übersetzung fehlgeschlagen: ${response.status}`);
  }

  const data = await response.json();

  if (!Array.isArray(data) || !Array.isArray(data[0])) {
    return text;
  }

  return data[0]
    .map((part) => {
      if (Array.isArray(part) && typeof part[0] === "string") {
        return part[0];
      }

      return "";
    })
    .join("")
    .trim() || text;
}

function splitTextIntoChunks(text, maxLength) {
  if (text.length <= maxLength) {
    return [text];
  }

  const sentences = text.split(/(?<=[.!?])\s+/);
  const chunks = [];
  let currentChunk = "";

  for (const sentence of sentences) {
    if ((currentChunk + " " + sentence).trim().length <= maxLength) {
      currentChunk = (currentChunk + " " + sentence).trim();
    } else {
      if (currentChunk) {
        chunks.push(currentChunk);
      }

      if (sentence.length > maxLength) {
        chunks.push(...splitLongText(sentence, maxLength));
        currentChunk = "";
      } else {
        currentChunk = sentence;
      }
    }
  }

  if (currentChunk) {
    chunks.push(currentChunk);
  }

  return chunks;
}

function splitLongText(text, maxLength) {
  const chunks = [];

  for (let i = 0; i < text.length; i += maxLength) {
    chunks.push(text.slice(i, i + maxLength));
  }

  return chunks;
}