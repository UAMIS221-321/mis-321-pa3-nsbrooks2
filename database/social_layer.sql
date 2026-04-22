-- SummitScout social layer: profiles, follows, trail-tagged posts, feed.
-- Run after schema.sql (requires `trails` table).

USE pa_app;

CREATE TABLE IF NOT EXISTS social_profiles (
  id            CHAR(36)      NOT NULL,
  handle        VARCHAR(64)   NOT NULL,
  display_name  VARCHAR(120)  NOT NULL,
  bio           VARCHAR(500)  NOT NULL DEFAULT '',
  avatar_hue    SMALLINT UNSIGNED NOT NULL DEFAULT 210,
  created_at    TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uq_social_handle (handle)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS social_follows (
  follower_id   CHAR(36)      NOT NULL,
  following_id  CHAR(36)      NOT NULL,
  created_at    TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (follower_id, following_id),
  KEY ix_social_follows_following (following_id),
  CONSTRAINT fk_sf_follower FOREIGN KEY (follower_id) REFERENCES social_profiles (id) ON DELETE CASCADE,
  CONSTRAINT fk_sf_following FOREIGN KEY (following_id) REFERENCES social_profiles (id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS social_posts (
  id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  author_id     CHAR(36)        NOT NULL,
  caption       TEXT            NOT NULL,
  trail_id      BIGINT UNSIGNED NULL,
  created_at    TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY ix_social_posts_author_created (author_id, created_at DESC),
  CONSTRAINT fk_sp_author FOREIGN KEY (author_id) REFERENCES social_profiles (id) ON DELETE CASCADE,
  CONSTRAINT fk_sp_trail FOREIGN KEY (trail_id) REFERENCES trails (id) ON DELETE SET NULL
) ENGINE=InnoDB;

-- Demo hikers (fixed UUIDs for cookie / API tests)
INSERT IGNORE INTO social_profiles (id, handle, display_name, bio, avatar_hue) VALUES
('a1000000-0000-4000-8000-000000000001', 'alex_ridge', 'Alex Rivera', 'Dog-friendly loops & ridge sunrises · Eastern Ridge local', 168),
('a1000000-0000-4000-8000-000000000002', 'morgan_peaks', 'Morgan Lee', 'Training for bigger vert · always checking Open-Meteo first', 312),
('a1000000-0000-4000-8000-000000000003', 'sam_riverwalk', 'Sam Okonkwo', 'Tuscaloosa trails + riverwalk miles · photos every Tuesday', 24),
('a1000000-0000-4000-8000-000000000004', 'riley_switchback', 'Riley Navarro', 'Moderate mileage / max vibes · gear nerd', 275),
('a1000000-0000-4000-8000-000000000005', 'summitscout', 'SummitScout', 'Official tips, safety reminders & picks from our trail catalog', 145);

-- Follow graph: everyone follows the official account; cross-follows for a fuller feed
INSERT IGNORE INTO social_follows (follower_id, following_id) VALUES
('a1000000-0000-4000-8000-000000000001', 'a1000000-0000-4000-8000-000000000005'),
('a1000000-0000-4000-8000-000000000002', 'a1000000-0000-4000-8000-000000000005'),
('a1000000-0000-4000-8000-000000000003', 'a1000000-0000-4000-8000-000000000005'),
('a1000000-0000-4000-8000-000000000004', 'a1000000-0000-4000-8000-000000000005'),
('a1000000-0000-4000-8000-000000000001', 'a1000000-0000-4000-8000-000000000002'),
('a1000000-0000-4000-8000-000000000002', 'a1000000-0000-4000-8000-000000000001'),
('a1000000-0000-4000-8000-000000000003', 'a1000000-0000-4000-8000-000000000004'),
('a1000000-0000-4000-8000-000000000004', 'a1000000-0000-4000-8000-000000000003');

INSERT INTO social_posts (author_id, caption, trail_id)
SELECT 'a1000000-0000-4000-8000-000000000005',
       'Pick of the week: shaded cedar bowl with creek access — great for dogs early season.',
       id FROM trails WHERE slug = 'cedar-basin-loop' LIMIT 1;

INSERT INTO social_posts (author_id, caption, trail_id)
SELECT 'a1000000-0000-4000-8000-000000000001',
       'Tuesday AM was empty. Mist stayed in the trees until mile 3 — perfect.',
       id FROM trails WHERE slug = 'mist-creek-trail' LIMIT 1;

INSERT INTO social_posts (author_id, caption, trail_id)
SELECT 'a1000000-0000-4000-8000-000000000002',
       'Hard day on talus but the saddle view paid off. Not for dogs past mile 2.',
       id FROM trails WHERE slug = 'granite-saddle' LIMIT 1;

INSERT INTO social_posts (author_id, caption, trail_id)
SELECT 'a1000000-0000-4000-8000-000000000003',
       'Riverwalk miles with coffee — low vert recovery day.',
       id FROM trails WHERE slug = 'tuscaloosa-riverwalk' LIMIT 1;

INSERT INTO social_posts (author_id, caption, trail_id)
SELECT 'a1000000-0000-4000-8000-000000000004',
       'Lake loop + birding window — roots are slick after rain.',
       id FROM trails WHERE slug = 'lake-nicol-shore' LIMIT 1;

INSERT INTO social_posts (author_id, caption, trail_id)
SELECT 'a1000000-0000-4000-8000-000000000005',
       'Save this for a quiet weekday: old fire road is predictable footing for new hikers.',
       id FROM trails WHERE slug = 'old-fire-road' LIMIT 1;

INSERT INTO social_posts (author_id, caption, trail_id)
SELECT 'a1000000-0000-4000-8000-000000000001',
       'Quick gear shakeout on the cutoff — shared with bikes so ears up.',
       id FROM trails WHERE slug = 'old-fire-road' LIMIT 1;
