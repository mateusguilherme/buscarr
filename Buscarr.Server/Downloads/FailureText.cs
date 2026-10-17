namespace Buscarr.Server.Downloads;

public static class FailureText
{
    public static string Describe(string reason) =>
        reason switch
        {
            "refused" => "o peer recusou",
            "stalled" => "parou de responder",
            "transfer-failed" => "a transferência falhou",
            "missing-files" => "os arquivos baixados não foram encontrados",
            "wrong-tracks" => "as faixas não batem com a edição",
            "bad-audio" => "o áudio veio com defeito",
            "fake-audio" => "a qualidade anunciada é falsa",
            "transcode-failed" => "a conversão falhou",
            "import-failed" => "o import na biblioteca falhou",
            "rejected" => "nenhuma pasta passou na verificação",
            "nothing-found" => "nada encontrado",
            "no-matching-folder" => "nenhuma pasta serve",
            _ => reason,
        };
}
