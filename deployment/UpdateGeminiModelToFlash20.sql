-- Point Gemini at a stable model (gemini-flash-latest was 503 / timeout heavy).
UPDATE m
SET m.ModelName = N'gemini-2.0-flash',
    m.MaxContextTokens = 1000000
FROM AIModels m
INNER JOIN AIProviders p ON p.AIProviderId = m.AIProviderId
WHERE p.Name = N'Gemini'
  AND m.IsActive = 1
  AND m.ModelName <> N'gemini-2.0-flash';
