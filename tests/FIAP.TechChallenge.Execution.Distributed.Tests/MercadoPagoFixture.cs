namespace FIAP.TechChallenge.Execution.Distributed.Tests;

// Isolated HTTP replacement for the provider; Billing and its webhook remain real.
internal static class MercadoPagoFixture
{
    public const string Server = """
        const http = require('node:http');
        const orders = new Map();
        http.createServer(async (req, res) => {
          res.setHeader('Content-Type', 'application/json');
          if (req.method === 'POST' && req.url === '/v1/orders') {
            let body = ''; for await (const chunk of req) body += chunk;
            const data = JSON.parse(body), id = 'ORD-' + data.external_reference;
            if (!orders.has(id)) orders.set(id, {id, external_reference: data.external_reference,
              total_amount: data.total_amount, currency: 'BRL', status: 'processed', status_detail: 'accredited'});
            res.statusCode = 201;
            return res.end(JSON.stringify({id, checkout_url: 'https://www.mercadopago.com.br/checkout/test?order_id=' + id}));
          }
          const order = orders.get(decodeURIComponent(req.url.split('/').pop()));
          if (order) return res.end(JSON.stringify(order));
          res.statusCode = 404; res.end('{}');
        }).listen(8080, '0.0.0.0');
        """;
}
