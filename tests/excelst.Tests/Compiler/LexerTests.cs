using Excelst.Compiler;

namespace Excelst.Tests.Compiler;

public class LexerTests
{
    private static List<Token> Tokenize(string source) => new Lexer(source).Tokenize();

    // ── Tokens de base ────────────────────────────────────────────────────────

    [Fact]
    public void Identifiant_simple()
    {
        var tokens = Tokenize("sheets");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("sheets", tokens[0].Value);
    }

    [Fact]
    public void Chaine_simple()
    {
        var tokens = Tokenize("\"Budget\"");
        Assert.Equal(TokenKind.String, tokens[0].Kind);
        Assert.Equal("Budget", tokens[0].Value);
    }

    [Fact]
    public void Entier()
    {
        var tokens = Tokenize("42");
        Assert.Equal(TokenKind.Integer, tokens[0].Kind);
        Assert.Equal("42", tokens[0].Value);
    }

    [Fact]
    public void Decimal()
    {
        var tokens = Tokenize("3.1415927");
        Assert.Equal(TokenKind.Float, tokens[0].Kind);
        Assert.Equal("3.1415927", tokens[0].Value);
    }

    [Fact]
    public void Entier_suivi_d_un_point_est_deux_tokens()
    {
        var tokens = Tokenize("42.");
        Assert.Equal(TokenKind.Integer, tokens[0].Kind);
        Assert.Equal(TokenKind.Dot,     tokens[1].Kind);
        Assert.Equal(TokenKind.Eof,     tokens[2].Kind);
    }

    [Fact]
    public void Ponctuation()
    {
        var tokens = Tokenize(".(),{}:");
        Assert.Equal(TokenKind.Dot,    tokens[0].Kind);
        Assert.Equal(TokenKind.LParen, tokens[1].Kind);
        Assert.Equal(TokenKind.RParen, tokens[2].Kind);
        Assert.Equal(TokenKind.Comma,  tokens[3].Kind);
        Assert.Equal(TokenKind.LBrace, tokens[4].Kind);
        Assert.Equal(TokenKind.RBrace, tokens[5].Kind);
        Assert.Equal(TokenKind.Colon,  tokens[6].Kind);
        Assert.Equal(TokenKind.Eof,    tokens[7].Kind);
    }

    // ── Positions ─────────────────────────────────────────────────────────────

    [Fact]
    public void Position_ligne_et_colonne()
    {
        var tokens = Tokenize("sheets\nadd");
        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(1, tokens[0].Column);
        Assert.Equal(2, tokens[1].Line);
        Assert.Equal(1, tokens[1].Column);
    }

    // ── Commentaires ──────────────────────────────────────────────────────────

    [Fact]
    public void Commentaire_ligne_ignore()
    {
        var tokens = Tokenize("// ceci est un commentaire\nsheets");
        Assert.Equal(TokenKind.Identifier, tokens[0].Kind);
        Assert.Equal("sheets", tokens[0].Value);
    }

    [Fact]
    public void Commentaire_en_fin_de_ligne_ignore()
    {
        var tokens = Tokenize("sheets // commentaire");
        Assert.Single(tokens, t => t.Kind == TokenKind.Identifier);
    }

    // ── Token EOF ─────────────────────────────────────────────────────────────

    [Fact]
    public void Source_vide_retourne_eof()
    {
        var tokens = Tokenize("");
        Assert.Single(tokens);
        Assert.Equal(TokenKind.Eof, tokens[0].Kind);
    }

    // ── Erreurs ───────────────────────────────────────────────────────────────

    [Fact]
    public void Chaine_non_terminee_leve_exception()
    {
        Assert.Throws<LexerException>(() => Tokenize("\"non terminée"));
    }

    [Fact]
    public void Chaine_avec_saut_de_ligne_leve_exception()
    {
        Assert.Throws<LexerException>(() => Tokenize("\"ligne\n1\""));
    }

    [Fact]
    public void Caractere_inconnu_leve_exception()
    {
        Assert.Throws<LexerException>(() => Tokenize("@"));
    }
}
