const express = require('express');
const cors = require('cors');
const jsonServer = require('json-server');

const server = express();
server.use(cors());

const port = parseInt(process.env.PORT, 10) || 3000;
server.use(express.static('./public'));
server.use('/api', jsonServer.router('db.json'));

server.listen(port, () => console.log(`listening on port ${port}`));
