# Resultados de execução

Esta pasta recebe apenas saídas reais. Em 2026-09-06, após aplicar as migrations EF existentes e confirmar HTTP 200 em `/api/room`, os testes k6 básico e progressivo foram executados com sucesso.

- `k6-basic.txt`: saída integral do teste com 5 VUs por 20 segundos.
- `k6-load.txt`: saída integral do teste progressivo até 25 VUs.
- `k6-summary.md`: métricas extraídas e conclusão comparativa.
