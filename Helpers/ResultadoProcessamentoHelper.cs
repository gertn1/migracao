using PrecificacaoPoc.Domain;

namespace PrecificacaoPoc.Helpers;

/// <summary>Preenche o ResultadoProcessamento com o código e a descrição do retorno final (equivalente ao @numRetorno da procedure).</summary>
public static class ResultadoProcessamentoHelper
{
    public static void Finalizar(ResultadoProcessamento r, RetornoProcessamento ret)
    {
        r.StatusRetorno = (int)ret;
        r.Descricao = ret switch
        {
            RetornoProcessamento.Sucesso => "Sucesso",
            RetornoProcessamento.BoletimNaoExiste => "Boletim não existe",
            RetornoProcessamento.ParametroPrecoNaoCadastrado => "Parâmetro de preço não cadastrado",
            RetornoProcessamento.ErroCalculoPreco9 => "Erro no cálculo de preços (Preço 9)",
            RetornoProcessamento.ErroAtualizacaoProdutoFilial => "Erro na atualização do produto_filial",
            RetornoProcessamento.ErroUpdateCabecalhoRegrasSempre => "Erro no update do cabeçalho desc_produto_cab (regras sempre)",
            RetornoProcessamento.ErroInclusaoCabecalhoRegrasSempre => "Erro na inclusão do cabeçalho desc_produto_cab (regras sempre)",
            RetornoProcessamento.ErroInclusaoDetalheRegrasSempre => "Erro na inclusão do detalhe desc_produto_det (regras sempre)",
            RetornoProcessamento.ErroInclusaoClientesOuDeleteDetalheRegrasSempre => "Erro na inclusão de clientes ou delete do detalhe desc_produto_det (regras sempre)",
            RetornoProcessamento.ErroInclusaoFinalizadoresOuDeleteCabecalhoRegrasSempre => "Erro na inclusão de finalizadores ou delete do cabeçalho desc_produto_cab (regras sempre)",
            RetornoProcessamento.ErroCategoriasOuDeleteFinalizadoresRegrasSempre => "Erro em categorias ou delete de finalizadores (regras sempre)",
            _ => $"Retorno desconhecido ({(int)ret})"
        };
    }
}
