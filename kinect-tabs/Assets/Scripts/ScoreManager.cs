using UnityEngine;

public class ScoreManager : MonoBehaviour
{
    public int winningScore = 10;

    public int player1Score;
    public int player2Score;

    private bool gameOver;

    public void AddPoint(int attackingPlayerId)
    {
        if (gameOver)
        {
            return;
        }

        if (attackingPlayerId == 1)
        {
            player1Score++;
        }
        else if (attackingPlayerId == 2)
        {
            player2Score++;
        }

        Debug.Log($"Score: Player 1 = {player1Score}, Player 2 = {player2Score}");

        if (player1Score >= winningScore)
        {
            gameOver = true;
            Debug.Log("Player 1 wins!");
        }
        else if (player2Score >= winningScore)
        {
            gameOver = true;
            Debug.Log("Player 2 wins!");
        }
    }
}