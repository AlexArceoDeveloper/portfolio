CREATE EXTENSION IF NOT EXISTS vector;

CREATE TABLE IF NOT EXISTS knowledge_documents (
    id text PRIMARY KEY,
    title text NOT NULL,
    content text NOT NULL,
    source_url text NOT NULL,
    embedding vector(64) NOT NULL
);

CREATE INDEX IF NOT EXISTS knowledge_documents_embedding_idx
    ON knowledge_documents
    USING hnsw (embedding vector_cosine_ops);
