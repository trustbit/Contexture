# Contexture Frontend

The Contexture frontend is written in [Vue.js](https://vuejs.org/).

## Requirements

- Contexture API on port 5000
- npm > 8.x

## Development

Recommended editor is VSCode. For the optimal development experience install all plugins
in [.vscode/extensions.json](.vscode/extensions.json)

### Start the application

```bash
npm i
npm run dev
```

### Build the application

```bash
npm i
npm run build
```

## Configuration

See [Env variables](.env)

| Key                          | Default Value | Description                                                                |
|------------------------------|---------------|----------------------------------------------------------------------------|
| VITE_CONTEXTURE_API_BASE_URL | ""            | The base url of the API. Defaults to blank string to use the reverse proxy |

### Helpful links

[Tailwind Cheat Sheet](https://nerdcave.com/tailwind-cheat-sheet)
