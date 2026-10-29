# Explicação dos métodos da migração

Documento para explicar, em linguagem simples, por que cada método existe na POC e a que parte da procedure
`sp_CsmPrecoAtlzBoletim` ele corresponde. Novos métodos entram no final.

---

## `PriceBulletinMapper.Mesclar`

**Onde está:** `Helpers/PriceBulletinMapper.cs`, chamado em `BoletimPrecoService.ProcessarPorFilialAsync`.

### Por que existe

Na procedure, a **Fase 3** faz um `JOIN` entre `BOLETIM_PRECO_DET` e `PRODUTO_FILIAL`/`PRODUTO_MESTRE`.
Isso traz, na mesma linha, os preços novos do boletim e o estado atual do produto na filial
(custos, preço 9 atual, se é monitorado). Os cálculos e o `UPDATE` dependem dos dois lados.

Na POC, o boletim chega como objeto da aplicação, sem passar pelo banco. Então o `JOIN` ficou em duas partes:

1. O repositório (`ObterEstadoProdutoFilialAsync`) lê o estado atual de `PRODUTO_FILIAL`.
2. O `Mesclar` junta esse estado com os dados do boletim.

Em uma frase: **o `Mesclar` é o `JOIN` da Fase 3 da procedure, refeito em C# porque o boletim vem em memória e não de uma tabela.**

### Por que vem antes dos cálculos

Os cálculos usam colunas de `PRODUTO_FILIAL` e `PRODUTO_MESTRE`:

- **Custo:** `PRFI_VL_UCG` e `PRFI_VL_CMPG`.
- **Preço 9:** `PRFI_VL_PRCMAXCONS`, `PRFI_VL_PRECO9` e `PRME_FL_MONITORADO`.
- **Margens:** dependem do custo e do preço 9.

Sem a mesclagem, o `Calcular` não teria esses dados. Na procedure isso acontece no mesmo `INSERT ... SELECT`.

### O que ele faz, passo a passo

| Passo | Equivalente na procedure |
|---|---|
| Só mantém produtos que existem em `PRODUTO_FILIAL` | `INNER JOIN PRODUTO_FILIAL` |
| Só mantém produtos que existem em `PRODUTO_MESTRE` | `INNER JOIN PRODUTO_MESTRE` (feito na query do repositório) |
| Descarta produtos de `FUTURO_PRECO_DUPLICADO` | `NOT EXISTS (...)` (feito na query do repositório) |
| Copia os campos do boletim para o item | colunas `BOPD_*` da `#BoletimCalc` |
| Arredonda para a precisão da coluna (2 ou 4 casas) | conversão de tipo ao inserir na `#BoletimCalc` |
| Falha se o produto estiver repetido no boletim | `PRIMARY KEY (BOPD_CD_PRODUTO)` da `#BoletimCalc` |

### Detalhes de implementação

- Usa um `Dictionary` por `ProdutoId`, em vez de procurar na lista a cada item (evita custo O(n·m) em boletins grandes).
- O método auxiliar `Numeric` só trata `decimal?` e chama `PrecoCalculator.ArredondarNumeric`
  (arredondamento "metade para longe do zero", igual ao `CONVERT(NUMERIC)` do SQL Server).

### Pontos de atenção

- Produto repetido no boletim lança `InvalidOperationException`, como a violação de PK na procedure.
- Falta validar com dados reais: rodar a procedure e o C# com o mesmo boletim e comparar `PRODUTO_FILIAL`.
