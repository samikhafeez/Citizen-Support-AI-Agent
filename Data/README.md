# Data Folder

This folder contains the FAQ source data for the chatbot's in-process vector search.

## Included

| File | Description | Size |
|---|---|---|
| `faqs.json` | Hand-authored FAQ entries across all 12 council service areas | ~64 KB |

## Excluded from this repository (regenerate locally)

The following files are either too large for GitHub or are generated artefacts
that can be recreated from the ingestion pipeline:

| File | Reason | How to regenerate |
|---|---|---|
| `bradford_pages.json` | ~8 MB scraped web content | Run `LangChainService/scrape_bradford.py` |
| `bradford_targeted_pages.json` | ~5 MB scraped web content | Run `LangChainService/scrape_bradford_targeted.py` |
| `chunks.embeddings.json` | Generated embedding cache | Deleted automatically on app restart when absent |
| `LangChainService/data/faiss_index/` | ~8 MB FAISS binary index | Run `LangChainService/ingest.py` |

## Regenerating the FAISS index

```bash
# 1. Start the embedding service
cd EmbeddingService
pip install -r requirements.txt
uvicorn app:app --host 0.0.0.0 --port 8001 &

# 2. Scrape Bradford Council pages (requires internet access)
cd ../LangChainService
pip install -r requirements.txt
python scrape_bradford_targeted.py   # produces data/bradford_targeted_pages.json

# 3. Build the FAISS index
EMBEDDING_URL=http://localhost:8001/embed python ingest.py
# → writes LangChainService/data/faiss_index/index.faiss + index.pkl
```

The `chunks.embeddings.json` FAQ embedding cache is rebuilt automatically
when the .NET app starts if the file is absent or outdated.
