# Explicação dos métodos da migração

Documento para explicar, em linguagem simples, por que cada método existe na POC e a que parte da procedure
`sp_CsmPrecoAtlzBoletim` ele corresponde. Novos métodos entram no final.

---

## O JOIN da Fase 3: `ObterEstadoProdutoFilialAsync` + junção em `ProcessarPorFilialAsync`

**Onde está:** `Infrastructure/BoletimRepository.cs` (consulta) e `Services/BoletimPrecoService.cs` (`ProcessarPorFilialAsync`, a junção com dicionário e `foreach`).

### Por que existe

Na procedure, a **Fase 3** faz um `JOIN` entre `BOLETIM_PRECO_DET` e `PRODUTO_FILIAL`/`PRODUTO_MESTRE`.
Isso traz, na mesma linha, os preços novos do boletim e o estado atual do produto na filial
(custo, PMC, preço 9 atual, se é monitorado). Os cálculos dependem dos dois lados.

Na POC, o boletim chega como objeto da aplicação, sem passar pelo banco. Então o `JOIN` ficou em duas partes:

1. O repositório (`ObterEstadoProdutoFilialAsync`) lê o estado dos produtos do boletim e devolve `EstadoProdutoFilial`.
2. O serviço junta, por `ProdutoId`, os itens do boletim (`ItemBoletim`) com esses estados, gerando `ItemComEstado`.

Em uma frase: **a consulta mais essa junção são o `JOIN` da Fase 3 da procedure, refeito em C# porque o boletim vem em memória e não de uma tabela.**

### Qual trecho da procedure ele substitui

O `SELECT` de `ObterEstadoProdutoFilialAsync` é o equivalente do `SELECT` do `INSERT INTO #BoletimCalc` da Fase 3, no que diz respeito às tabelas do banco.
Esse trecho da procedure faz três coisas ao mesmo tempo:

1. Parte de `BOLETIM_PRECO_DET d` e junta com `PRODUTO_FILIAL pf` (na filial processada) e com `PRODUTO_MESTRE pm`.
2. Exclui os produtos que estão em `COSMOSPRECOS.dbo.FUTURO_PRECO_DUPLICADO` (`NOT EXISTS`).
3. Usa colunas de `pf`/`pm` só dentro dos `CROSS APPLY` para calcular custo, Preço 9 e margens. Essas colunas **não** vão para a `#BoletimCalc`: ela guarda as colunas do boletim (`d.*`) mais os resultados (`PRECO9_CALC`, `MARGEM_PRECO9`, `MBE_PRCVEN`, `FL_ARREDONDA`).

Na POC, o que é do banco (itens 1 e 2 e a leitura das colunas do item 3) está em `ObterEstadoProdutoFilialAsync`:

| Procedure (Fase 3) | `ObterEstadoProdutoFilialAsync` |
|---|---|
| `INNER JOIN PRODUTO_FILIAL pf ON pf.FILI_CD_FILIAL = @COD_FILIAL ...` | `FROM PRODUTO_FILIAL pf ... WHERE pf.FILI_CD_FILIAL = @filialId` |
| `INNER JOIN PRODUTO_MESTRE pm ...` | `INNER JOIN PRODUTO_MESTRE pm ...` |
| `NOT EXISTS (... FUTURO_PRECO_DUPLICADO ...)` | O mesmo `NOT EXISTS` |
| `pf.PRFI_VL_UCG`, `PRFI_VL_CMPG` no `CROSS APPLY` de custo | `Ucg`, `Cmpg` |
| `pf.PRFI_VL_PRCMAXCONS`, `PRFI_VL_PRECO9` e `pm.PRME_FL_MONITORADO` no `CROSS APPLY` do Preço 9 | `PrcMaxCons`, `Preco9`, `Monitorado` |

O que **não** está nessa consulta e continua em outro lugar:

- Os cálculos dos `CROSS APPLY` (custo, Preço 9, margens, `FL_ARREDONDA`) estão em C#, em `PrecoCalculator`.
- A origem das linhas (`BOLETIM_PRECO_DET d`) é o boletim em memória, e o `foreach` em `ProcessarPorFilialAsync` junta os dois lados.
- O filtro `pf.PRME_CD_PRODUTO IN @ids` só existe na POC. Na procedure, o `JOIN` com o `DET` já limita aos produtos do boletim. Aqui ele faz esse papel, e por isso a consulta roda em blocos de 1000 ids.

Resumindo para quem pergunta: a procedure faz tudo em um `SELECT` só, porque o boletim e o produto estão no mesmo banco. Na POC o boletim chega em memória, então separei em duas partes: uma consulta para trazer o que o banco sabe do produto e uma junção em C# (dicionário + `foreach`) para juntar com o boletim. As regras (quais produtos entram, quais colunas alimentam o cálculo) são as mesmas.

### Juntando o boletim com o estado (o "JOIN" em C#)

Depois da consulta, o serviço tem duas listas separadas: os itens do boletim e os estados que o banco devolveu.
Este trecho junta as duas, produto a produto:

```csharp
var estadoPorProduto = estados.ToDictionary(e => e.ProdutoId);

var itens = new List<ItemComEstado>();
foreach (var item in boletim)
{
    if (!estadoPorProduto.ContainsKey(item.ProdutoId))
        continue;

    var estado = estadoPorProduto[item.ProdutoId];
    itens.Add(new ItemComEstado(item, estado));
}
```

**Como eu explicaria para uma pessoa:**

1. **Monto um índice.** `ToDictionary` cria uma "lista telefônica": dado o código do produto, acho o estado dele direto, sem folhear a lista inteira toda vez.
2. **Passo item por item do boletim.** Para cada um, pergunto: "esse produto voltou do banco?"
3. **Se não voltou, pulo** (`continue`). Isso acontece quando o produto não existe na filial, não existe em `PRODUTO_MESTRE` ou está em `FUTURO_PRECO_DUPLICADO`. Ele sai do processamento, sem erro.
4. **Se voltou, junto os dois** em um `ItemComEstado`: o item (dados do boletim) com o estado (dados do banco). É uma linha pronta para o cálculo.

**O que é na procedure:** é a junção do `INSERT INTO #BoletimCalc ... SELECT` da Fase 3, ou seja, o `INNER JOIN` entre `BOLETIM_PRECO_DET` e `PRODUTO_FILIAL`/`PRODUTO_MESTRE`.
Quem não tem par nos dois lados não entra, tanto lá quanto aqui. Cada `ItemComEstado` é uma linha da `#BoletimCalc` antes dos cálculos.

**Exemplo:** o boletim tem os produtos 10, 20 e 30, e o banco devolveu estado só do 10 e do 30.
O resultado são dois pares (10 e 30). O produto 20 fica de fora, como o `INNER JOIN` da procedure o deixaria.

**Por que dicionário e não procurar na lista:** com milhares de produtos, procurar na lista inteira para cada item ficaria lento. O dicionário acha direto pelo código.
O `ToDictionary` não aceita código repetido, e aqui não repete: a consulta devolve uma linha por produto na filial.

### Por que o estado é lido

Os preços novos vêm do boletim e vão direto para os `UPDATE`s. A consulta busca só o que o **cálculo** precisa e o boletim não traz:

| Campo | Para que serve |
|---|---|
| `Ucg` (`PRFI_VL_UCG`), `Cmpg` (`PRFI_VL_CMPG`) | Custo, que entra nas margens |
| `PrcMaxCons` (`PRFI_VL_PRCMAXCONS`) | Preço 9 |
| `Preco9` (`PRFI_VL_PRECO9`) | Preço 9 atual: é o valor mantido em alguns casos |
| `Monitorado` (`PRODUTO_MESTRE.PRME_FL_MONITORADO`) | Preço 9: decide entre PMC e preço + 5% |

O restante de `PRODUTO_FILIAL` não é lido: os `UPDATE`s só gravam nas colunas que o boletim altera.

### O que cada parte faz

| Parte | Equivalente na procedure |
|---|---|
| Consulta só devolve produtos que existem em `PRODUTO_FILIAL` | `INNER JOIN PRODUTO_FILIAL` |
| Consulta só devolve produtos que existem em `PRODUTO_MESTRE` | `INNER JOIN PRODUTO_MESTRE` |
| Consulta descarta produtos de `FUTURO_PRECO_DUPLICADO` | `NOT EXISTS (...)` |
| O `foreach` descarta item do boletim que não voltou da consulta | efeito dos `JOIN`s acima: o item some do processamento |
| `PriceBulletinMapper.ParaItens` arredonda para a precisão da coluna (2 ou 4 casas) | conversão de tipo ao inserir na `#BoletimCalc` |
| `ParaItens` falha se o produto estiver repetido no boletim | `PRIMARY KEY (BOPD_CD_PRODUTO)` da `#BoletimCalc` |

### Detalhes de implementação

- `ObterEstadoProdutoFilialAsync` consulta em blocos de 1000 ids, porque o Dapper cria um parâmetro por id e o SQL Server aceita no máximo 2100.
- `ItemBoletim` guarda só dados do boletim; `EstadoProdutoFilial` guarda só dados do banco. Cada tipo mostra a origem do dado.
- O arredondamento usa `PrecoCalculator.ArredondarNumeric` ("metade para longe do zero", igual ao `CONVERT(NUMERIC)` do SQL Server).

### Pontos de atenção

- Produto repetido no boletim lança `InvalidOperationException`, como a violação de PK na procedure.
- Falta validar com dados reais: rodar a procedure e o C# com o mesmo boletim e comparar `PRODUTO_FILIAL`.
