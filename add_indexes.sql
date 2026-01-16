-- Add performance indexes to existing database
CREATE INDEX IF NOT EXISTS "IX_Swimmers_Name" ON "Swimmers" ("Name");
CREATE INDEX IF NOT EXISTS "IX_Events_Stroke_DistanceMeters" ON "Events" ("Stroke", "DistanceMeters");
CREATE INDEX IF NOT EXISTS "IX_Results_SwimmerId_EventId_Date" ON "Results" ("SwimmerId", "EventId", "Date");
CREATE INDEX IF NOT EXISTS "IX_Results_Date" ON "Results" ("Date");
