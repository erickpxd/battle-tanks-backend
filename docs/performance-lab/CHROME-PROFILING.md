# Profiling do Canvas no Chrome DevTools

## Preparação

1. Suba PostgreSQL, Redis, EMQX, backend e frontend conforme o relatório principal.
2. Abra `http://localhost:4200`, autentique-se, entre em uma sala e abra a tela do jogo com o Canvas visível.
3. Se possível, abra uma segunda janela/jogador para haver movimento remoto e colisões.
4. Feche outras abas pesadas. Mantenha o zoom do Chrome em 100% e anote o hardware, a versão do Chrome e o `devicePixelRatio` (`window.devicePixelRatio` no Console).

## Gravação

1. Pressione **F12**.
2. Abra **Performance**.
3. Ative **Screenshots**. Em **Capture settings**, mantenha **Web Vitals** e a faixa de memória apenas se quiser correlacionar alocações; não aplique CPU throttling na primeira captura.
4. Clique em **Record** (círculo) ou pressione `Ctrl+E`.
5. Volte ao jogo e movimente o tanque continuamente por **10 segundos**.
6. Durante os mesmos 10 segundos, atire algumas vezes, incluindo contra paredes destrutíveis e, se houver outro jogador, contra o tanque remoto.
7. Volte ao DevTools e clique em **Stop** ou pressione `Ctrl+E`.
8. Salve a gravação pelo botão de download da aba Performance (`battle-tanks-canvas-profile.json.gz`).

## Leitura objetiva

1. Na visão geral, selecione com o mouse apenas o intervalo dos 10 segundos de interação.
2. Observe a faixa **Frames**. Um frame acima de aproximadamente `16,7 ms` não sustenta 60 FPS; vários frames consecutivos elevados indicam travamento perceptível.
3. Passe o mouse nos frames altos para registrar **FPS/frame time**. Não confunda o FPS observado com uma garantia para outros dispositivos.
4. Na faixa **Main**, procure blocos com triângulo vermelho. Eles representam tarefas longas; expanda a pilha até localizar a função que consumiu tempo.
5. Use **Bottom-up** e ordene por **Self Time**; pesquise por `gameLoop`, `draw`, `drawExpandedMap`, `drawTerrainTile`, `drawTank`, `drawExplosions`, `updateProjectile` e `resolveProjectileCollision`.
6. Em **Call tree**, confirme se o custo parte do callback de `requestAnimationFrame`. Registre Self Time e Total Time da função mais cara.
7. Procure eventos **Layout**/**Recalculate Style** próximos de `centerViewportOnLocalTank`/`scrollTo`. Eles precisam aparecer na gravação para serem reportados como evidência; a inspeção estática apenas indica a possibilidade.
8. Compare uma segunda gravação com `devicePixelRatio` reduzido somente para diagnóstico: no DevTools, emulation/device toolbar pode simular outra tela. Não use essa comparação como resultado definitivo sem registrar a configuração.

## Screenshots obrigatórios

### Screenshot 1 — visão geral

- Aba Performance inteira após a gravação.
- Intervalo dos 10 segundos selecionado.
- Faixas **FPS**, **Frames** e **Main** visíveis.
- Identificação: `chrome-performance-01-overview.png`.

### Screenshot 2 — frame elevado

- Zoom em um trecho com frame time elevado.
- Faixa **Frames** e tooltip/detalhe com a duração do frame visíveis.
- Se não houver frame elevado, capture um frame representativo e escreva explicitamente que não foi observado frame lento nessa amostra.
- Identificação: `chrome-performance-02-frame.png`.

### Screenshot 3 — função mais custosa

- **Main Thread / flame chart** expandido no frame escolhido.
- Callback de `requestAnimationFrame` e a função mais custosa visíveis.
- Painel **Bottom-up** ou **Call tree** mostrando Self Time/Total Time.
- Identificação: `chrome-performance-03-flame-chart.png`.

### Screenshot 4 — Long Task ou ausência dela

- Um bloco com triângulo vermelho e sua pilha expandida.
- Se nenhuma Long Task existir, capture a faixa Main do intervalo completo e registre “nenhuma Long Task observada nesta gravação”.
- Identificação: `chrome-performance-04-long-task.png`.

Copie as imagens para `docs/performance-lab/evidence/` e substitua os marcadores correspondentes no relatório principal. Não atribua um gargalo a uma função apenas pela aparência do gráfico: confirme a seleção e os tempos nos painéis inferiores.
