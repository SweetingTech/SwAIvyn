# Agent seed data

Place runtime agent records in `agents.json` to pre-populate the
`agents` table when the application starts. The seed file should contain
an array of objects with the same fields returned by `/api/agents`, for
example:

```
[
  {
    "id": "search-indexer",
    "name": "Search Indexer",
    "status": "completed",
    "userId": "admin",
    "startedAt": "2024-06-01T12:00:00Z",
    "finishedAt": "2024-06-01T12:05:00Z",
    "meta": {"durationSeconds": 300}
  }
]
```

Leave the file empty (`[]`) if no seed data is required.
