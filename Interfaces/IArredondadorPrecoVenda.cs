namespace PrecificacaoPoc.Interfaces;

/// <summary>
/// [MIGRAÇÃO DA PROCEDURE - FASE 4]: Abstração para dbo.fn_ARREDONDA_PRECO_VENDA_MONITORADO(preco, filial).
/// O código-fonte da função NÃO foi fornecido na procedure; portanto a regra real de arredondamento
/// (faixas/terminações de preço por filial) precisa ser portada para uma implementação desta interface.
/// </summary>
public interface IArredondadorPrecoVenda
{
    decimal Arredondar(decimal preco, int filialId);
}
