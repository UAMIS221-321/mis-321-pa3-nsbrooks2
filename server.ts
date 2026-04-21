import express from 'express';
import { createServer as createViteServer } from 'vite';
import path from 'path';
import { fileURLToPath } from 'url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

async function startServer() {
  const app = express();
  const PORT = 3000;

  app.use(express.json());

  // In-memory store for saved trails (for demo purposes)
  let savedTrailIds: string[] = [];

  // API Routes
  app.post('/api/save-trail', (req, res) => {
    const { trailId } = req.body;
    if (trailId && !savedTrailIds.includes(trailId)) {
      savedTrailIds.push(trailId);
    }
    res.json({ success: true, savedTrailIds });
  });

  app.get('/api/saved-trails', (req, res) => {
    res.json({ savedTrailIds });
  });

  app.delete('/api/saved-trails/:id', (req, res) => {
    const { id } = req.params;
    savedTrailIds = savedTrailIds.filter(tid => tid !== id);
    res.json({ success: true, savedTrailIds });
  });

  // Vite middleware for development
  if (process.env.NODE_ENV !== 'production') {
    const vite = await createViteServer({
      server: { middlewareMode: true },
      appType: 'spa',
    });
    app.use(vite.middlewares);
  } else {
    // Production setup
    const distPath = path.join(process.cwd(), 'dist');
    app.use(express.static(distPath));
    app.get('*', (req, res) => {
      res.sendFile(path.join(distPath, 'index.html'));
    });
  }

  app.listen(PORT, '0.0.0.0', () => {
    console.log(`Server running at http://localhost:${PORT}`);
  });
}

startServer();
