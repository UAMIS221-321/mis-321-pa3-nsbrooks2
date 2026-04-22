-- =============================================================================
-- SummitScout — ONE script for Heroku JawsDB (single database they give you)
-- =============================================================================
-- 1. In JAWSDB_URL, the database name is the part AFTER the final "/" (before ? if any).
-- 2. Replace YOUR_JAWSDB_DATABASE below with that exact name (keep the backticks).
-- 3. In Workbench: connect → open this file → lightning bolt to run ALL once.
-- 4. Do not run twice: social_posts INSERTs would duplicate.
-- =============================================================================

USE `YOUR_JAWSDB_DATABASE`;

CREATE TABLE IF NOT EXISTS knowledge_documents (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  title           VARCHAR(512)    NOT NULL,
  content         MEDIUMTEXT      NOT NULL,
  source_label    VARCHAR(255)    NULL,
  created_at      TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at      TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  FULLTEXT KEY ft_knowledge (title, content)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS chat_sessions (
  id          CHAR(36)      NOT NULL,
  title       VARCHAR(255)  NOT NULL DEFAULT 'New chat',
  stage_json  JSON          NULL,
  created_at  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at  TIMESTAMP     NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS chat_messages (
  id              BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  session_id      CHAR(36)        NOT NULL,
  role            ENUM('user','assistant','system','tool') NOT NULL,
  content         MEDIUMTEXT      NOT NULL,
  metadata_json   JSON            NULL,
  created_at      TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY ix_chat_messages_session_created (session_id, created_at),
  CONSTRAINT fk_chat_messages_session
    FOREIGN KEY (session_id) REFERENCES chat_sessions (id)
    ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS app_users (
  id            BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  external_ref  VARCHAR(64)     NOT NULL,
  display_name  VARCHAR(255)    NOT NULL,
  email         VARCHAR(255)    NULL,
  notes         TEXT            NULL,
  is_active     TINYINT(1)      NOT NULL DEFAULT 1,
  created_at    TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  updated_at    TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uq_app_users_external_ref (external_ref),
  KEY ix_app_users_active (is_active)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS trails (
  id                    BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  slug                  VARCHAR(128)    NOT NULL,
  name                  VARCHAR(255)    NOT NULL,
  region                VARCHAR(128)    NOT NULL,
  dog_friendly          TINYINT(1)      NOT NULL DEFAULT 0,
  elevation_gain_ft     INT UNSIGNED    NOT NULL,
  length_mi             DECIMAL(5,2)    NOT NULL,
  difficulty            ENUM('easy','moderate','hard') NOT NULL DEFAULT 'moderate',
  crowd_calendar_note   VARCHAR(512)    NOT NULL DEFAULT '',
  trailhead_lat         DECIMAL(10,7)   NOT NULL,
  trailhead_lng         DECIMAL(10,7)   NOT NULL,
  guide_excerpt         MEDIUMTEXT      NOT NULL,
  safety_report         MEDIUMTEXT      NOT NULL,
  created_at            TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uq_trails_slug (slug),
  KEY ix_trails_dog_elev (dog_friendly, elevation_gain_ft),
  FULLTEXT KEY ft_trails (name, region, guide_excerpt, crowd_calendar_note)
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS hiker_gear_lists (
  session_id      CHAR(36)     NOT NULL,
  difficulty_tag  VARCHAR(64)  NOT NULL DEFAULT '',
  items_json      JSON         NOT NULL,
  updated_at      TIMESTAMP    NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (session_id),
  CONSTRAINT fk_gear_session FOREIGN KEY (session_id) REFERENCES chat_sessions (id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE IF NOT EXISTS safety_itineraries (
  id                         BIGINT UNSIGNED NOT NULL AUTO_INCREMENT,
  session_id                 CHAR(36)        NOT NULL,
  share_token                CHAR(24)        NOT NULL,
  trail_name                 VARCHAR(255)    NOT NULL,
  planner_name               VARCHAR(255)    NOT NULL,
  route_summary              TEXT            NOT NULL,
  planned_start              DATETIME        NULL,
  planned_return             DATETIME        NULL,
  emergency_contact_name     VARCHAR(255)    NOT NULL,
  emergency_contact_channel  VARCHAR(255)    NOT NULL,
  formal_body                MEDIUMTEXT      NOT NULL,
  created_at                 TIMESTAMP       NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uq_safety_share_token (share_token),
  KEY ix_safety_session (session_id),
  CONSTRAINT fk_safety_session FOREIGN KEY (session_id) REFERENCES chat_sessions (id) ON DELETE CASCADE
) ENGINE=InnoDB;

INSERT INTO knowledge_documents (title, content, source_label) VALUES
(
  'SummitScout Field Manual — Weather & Lightning',
  'Hyper-local forecasts must be interpreted at the trailhead coordinates, not town weather. Rapid cloud build after 09:00 on ridge lines is a go/no-go signal. If thunder roars, get off the peak: descend perpendicular to the ridge axis when possible. Wet rock on granite domes doubles slip risk—adjust pace and pole usage. Open-Meteo hourly data should be cross-checked with NWS if planning committing alpine objectives.',
  'summitscout-encyclopedia'
),
(
  'SummitScout Field Manual — Dogs, Regulations, and Etiquette',
  'Dog-friendly does not mean off-leash everywhere: default to 6ft leash unless signage explicitly allows voice control. Pack out waste bags even on “remote” trails—alpine tarns are sensitive. Yield to horses and uphill hikers. When users ask for nuanced combos (dog + elevation + crowd day), always query the trail catalog tool rather than inventing trail names.',
  'summitscout-encyclopedia'
),
(
  'SummitScout Field Manual — Safety Itineraries for Contacts',
  'Formal itineraries should include: route name, planned start/return window, last known cell coverage note, emergency contact channel, and “if overdue by X hours” instruction. Encourage sharing the read-only link with a trusted contact who is not on the trip. Prefer conservative return times.',
  'summitscout-encyclopedia'
),
(
  'SummitScout Field Manual — Gear by Difficulty',
  'Easy low-elevation loops: minimum 1L water, brim hat, map offline. Moderate: add traction aids if seasonally appropriate, 2L water, insulation layer. Hard: helmet where rockfall exists, headlamp with spare cells, emergency bivy, navigation redundancy. Use the gear checklist tool to persist a session-specific list the hiker can edit through dialogue.',
  'summitscout-encyclopedia'
),
(
  'SummitScout — Platform Overview',
  'SummitScout AI is an intelligent trail assistant. The Scout uses retrieval from this knowledge base, structured trail rows in MySQL, live Open-Meteo forecasts at trailheads, persisted gear checklists per chat session, and formal safety itineraries with shareable read-only links. Always prioritize safety over speed to the summit.',
  'bootstrap'
);

INSERT IGNORE INTO trails (slug, name, region, dog_friendly, elevation_gain_ft, length_mi, difficulty, crowd_calendar_note, trailhead_lat, trailhead_lng, guide_excerpt, safety_report) VALUES
('cedar-basin-loop','Cedar Basin Loop','Eastern Ridge',1,640,3.2,'easy','Weekday mornings—especially Tuesday—see very light use. Weekends moderate.','37.8654200','-119.4123000','Shaded cedar bowl with gentle grades and reliable creek at mile 1.8. Excellent for first-time peak baggers with dogs; keep dogs leashed at parking and first 0.5mi per land manager.','Flash-flood risk at creek after heavy rain. Snow patches possible Nov–Mar on north aspects.'),
('granite-saddle','Granite Saddle Spur','Eastern Ridge',0,1420,4.8,'hard','High traffic Sat/Sun AM; mid-week quieter but not empty.','37.8711000','-119.4055000','Class-2 talus past mile 2.2; poles recommended. No dogs on upper alpine section (seasonal restriction).','Lightning exposure on ridge after 10:00; turn around if cumulus builds. No reliable water above treeline.'),
('mist-creek-trail','Mist Creek Trail','North Cirque',1,920,5.5,'moderate','Tuesdays historically lowest use; Thu/Fri moderate.','37.8892000','-119.4310000','Dog-friendly with wide tread; two bridged crossings. Wildflower window late May.','Bridges ice-slick Dec–Feb. Bear activity reported Aug; carry spray, make noise.'),
('old-fire-road','Old Fire Road Cutoff','North Cirque',1,480,2.1,'easy','Popular with families weekends; Tuesday AM often nearly empty.','37.8820000','-119.4282000','Old service road—predictable footing, good for trail runners with dogs.','Shared with mountain bikes; uphill yield. Dust masks advised during dry spells.');

INSERT IGNORE INTO trails (slug, name, region, dog_friendly, elevation_gain_ft, length_mi, difficulty, crowd_calendar_note, trailhead_lat, trailhead_lng, guide_excerpt, safety_report) VALUES
('tuscaloosa-riverwalk','Black Warrior Riverwalk','Tuscaloosa, Alabama',1,95,2.4,'easy','Tuesday mornings are typically uncrowded; football home-game weekends very busy near campus bridges.','33.2098000','-87.5691000','Paved multi-use path along the Black Warrior; benches and river overlooks. Dogs welcome on leash; several shade trees.','Stay clear of river during flood watches; detour if gates closed.'),
('lake-nicol-shore','Lake Nicol Shore Loop','Tuscaloosa County, Alabama',1,380,3.6,'moderate','Weekdays light traffic; spring break sees more families.','33.2485000','-87.4812000','Rolling woods loop with lake views; roots and short climbs. Good birding spring–fall.','Ticks possible in tall grass—permethrin-treated clothing or thorough checks after.'),
('hurricane-creek-north','Hurricane Creek Park — North Loop','Near Tuscaloosa, Alabama',0,520,4.0,'moderate','Quieter early weekday; dogs not allowed on this segment per park signage (demo row).','33.3342000','-87.6185000','Bluff-line singletrack over limestone; creek crossings can run after rain.','Slippery rock when wet; turn back if water is high or muddy berms are rutted.');

INSERT IGNORE INTO app_users (external_ref, display_name, email, notes, is_active) VALUES
('demo-001', 'Alex Rivera', 'alex@example.com', 'Seed account for demos.', 1),
('demo-002', 'Jordan Chen', 'jordan@example.com', NULL, 1),
('demo-003', 'Sam Okonkwo', NULL, 'Inactive example row.', 0);

-- ----- Social layer (profiles, follows, posts) -----

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

INSERT IGNORE INTO social_profiles (id, handle, display_name, bio, avatar_hue) VALUES
('a1000000-0000-4000-8000-000000000001', 'alex_ridge', 'Alex Rivera', 'Dog-friendly loops & ridge sunrises · Eastern Ridge local', 168),
('a1000000-0000-4000-8000-000000000002', 'morgan_peaks', 'Morgan Lee', 'Training for bigger vert · always checking Open-Meteo first', 312),
('a1000000-0000-4000-8000-000000000003', 'sam_riverwalk', 'Sam Okonkwo', 'Tuscaloosa trails + riverwalk miles · photos every Tuesday', 24),
('a1000000-0000-4000-8000-000000000004', 'riley_switchback', 'Riley Navarro', 'Moderate mileage / max vibes · gear nerd', 275),
('a1000000-0000-4000-8000-000000000005', 'summitscout', 'SummitScout', 'Official tips, safety reminders & picks from our trail catalog', 145);

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
