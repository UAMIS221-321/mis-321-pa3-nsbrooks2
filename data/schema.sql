CREATE DATABASE IF NOT EXISTS trailscout;
USE trailscout;

CREATE TABLE IF NOT EXISTS trails (
    id VARCHAR(50) PRIMARY KEY,
    name VARCHAR(255) NOT NULL,
    location VARCHAR(255),
    difficulty VARCHAR(50),
    distanceMiles DOUBLE,
    elevationGainFeet INT,
    features TEXT,
    description TEXT,
    scenicViews BOOLEAN,
    waterfalls BOOLEAN,
    lakes BOOLEAN,
    crowdLevel VARCHAR(50)
);

CREATE TABLE IF NOT EXISTS saved_trails (
    trailId VARCHAR(50) PRIMARY KEY,
    savedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

-- Initial Data
INSERT IGNORE INTO trails (id, name, location, difficulty, distanceMiles, elevationGainFeet, features, description, scenicViews, waterfalls, lakes, crowdLevel) VALUES
('1', 'Emerald Bay State Park Trail', 'Lake Tahoe, CA', 'moderate', 4.5, 600, 'views,lake,historic site', 'A stunning trail offering panoramic views of Emerald Bay and Fannette Island.', true, false, true, 'busy'),
('2', 'Yosemite Falls Trail', 'Yosemite National Park, CA', 'hard', 7.2, 2700, 'waterfall,steep,views', 'One of Yosemite''s oldest historic trails, leading to the top of North America''s tallest waterfall.', true, true, false, 'busy');
