CREATE TABLE players (
    telegram_id   bigint      PRIMARY KEY,
    username      text        NOT NULL,
    first_seen_at timestamptz NOT NULL DEFAULT now(),
    last_login_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE solves (
    telegram_id    bigint      PRIMARY KEY REFERENCES players (telegram_id) ON DELETE CASCADE,
    -- Opaque public identifier, so certificate links do not expose Telegram ids.
    certificate_id text        NOT NULL UNIQUE,
    -- The handle as signed into the player's treasure code.
    username       text        NOT NULL,
    solved_at      timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX solves_order_idx ON solves (solved_at, telegram_id);
