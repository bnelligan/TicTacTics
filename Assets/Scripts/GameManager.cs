﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon;

public enum Player { P1, P2 };
public class GameManager : PunBehaviour {

    // Private vars
    int turn;
    Player activePlayer = Player.P1;
    Player localPlayer = Player.P1;

    // Config
    public bool InputEnabled = false;
    public bool GameRunning = false;
    public int boardSize = 3;
    public float moveDelay = 0.1f;
    public List<Move> MoveHistory; 
    // Access properties
    public Board GameBoard { get { return board; } }
    public UI_Manager UI { get { return ui; } } 
    public Player CurrentPlayer { get { return activePlayer; } }
    // External refs
    Board board;
    UI_Manager ui;

	// Use this for initialization
	void Start () {
        ui = GetComponent<UI_Manager>();
        board = FindObjectOfType<Board>();
        MoveHistory = new List<Move>();
        SetLocalPlayer();
	}
	
	// Update is called once per frame
	void Update () {
        if(GameRunning && InputEnabled)
        {
            HandleInput();
        }
        if(Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();
        }
	}

    private void SetLocalPlayer()
    {
        if (PhotonNetwork.isMasterClient)
            localPlayer = Player.P1;
        else
            localPlayer = Player.P2;
    }
    private void HandleInput()
    {
        if(Input.GetMouseButtonDown(0))
        {
            RaycastHit2D hit = Physics2D.Raycast(Camera.main.ScreenToWorldPoint(Input.mousePosition), Vector2.zero);
            if(hit)
            {
                Tile hitTile = hit.collider.GetComponent<Tile>();
                if(hitTile)
                {
                    if(IsMyTurn())
                    {
                        photonView.RPC("RPC_MakeMove", PhotonTargets.AllBuffered, hitTile.X, hitTile.Y, activePlayer);
                    }
                }
            }            
        }
    }
    public IEnumerator IE_StartWhenPlayersJoin()
    {
        while (PhotonNetwork.playerList.Length < 2)
        {
            yield return new WaitForEndOfFrame();
        }
        if (PhotonNetwork.isMasterClient)
        {
            photonView.RPC("RPC_StartGame", PhotonTargets.AllBuffered);
        }
    }

    [PunRPC] void RPC_MakeMove(int x, int y, Player player)
    {
        MakeMove(x, y, player);
    }
    public void MakeMove(Tile tile, Player player)
    {
        //Debug.Log("Tile clicked: " + tile.name);
        if(GameRunning)
        {
            if(board.MakeMove(tile, player))
            {
                // Record move
                Move move = new Move();
                move.turn = turn;
                move.tile = tile;
                move.player = player;
                Debug.Log("Turn: " + move.turn + " | Tile: (" + move.tile.X + "," + move.tile.Y + ") | Player: " + move.player.ToString());
                MoveHistory.Add(move);

                // Check if there is a winner
                Player winner;
                List<Tile> winningTiles;
                if(board.CheckWin(out winner, out winningTiles))
                {
                    int[,] winningTilesCoords = GetTileCoords(winningTiles);
                    if(PhotonNetwork.isMasterClient)
                    {
                        photonView.RPC("RPC_GameOver", PhotonTargets.All, winner, false, winningTilesCoords);
                    }
                    GameOver(winner, false);
                    ui.HighlightTiles(winningTiles);
                }
                else if(board.CheckFull())
                {
                    // Tie game
                    GameOver(winner, true);
                }
                else
                {
                    SwitchTurns();
                }            
            }
            else
            {
                Debug.Log("Tile already taken.");
            }
        }
        
    }
    public void MakeMove(int x, int y, Player player)
    {
        Tile tile = board.board[x, y];
        MakeMove(tile, player);
    }
    public void MakeMove(Move move)
    {
        MakeMove(move.tile, move.player);
    }
    public void MakeDelayedMoves(List<Move> moves)
    {
        StartCoroutine(IE_MakeDelayedMoves(moves));
    }
    private IEnumerator IE_MakeDelayedMoves(List<Move> moves)
    {
        InputEnabled = false;
        for (int m = 0; m < moves.Count; m++)
        {
            yield return new WaitForSeconds(moveDelay);
            MakeMove(moves[m]);
        }
        InputEnabled = true;
    }

    [PunRPC] void RPC_SwitchTurns(Player lastPlayer)
    {
        SwitchTurns(lastPlayer);
    }
    private void SwitchTurns()
    {
        turn++;
        // Flip the current player
        if(activePlayer == Player.P1)
        {
            activePlayer = Player.P2;
        }
        else if(activePlayer == Player.P2)
        {
            activePlayer = Player.P1;
        }
        // Update the UI
        ui.SetTurnText(activePlayer);
    }
    private void SwitchTurns(Player lastPlayer)
    {
        if (lastPlayer == Player.P1)
            activePlayer = Player.P2;
        else
            activePlayer = Player.P1;
    }

    [PunRPC] void RPC_StartGame()
    {
        StartGame();
    }
    public void StartGame()
    {
        if (MoveHistory != null) MoveHistory.Clear();
        else
        {
            MoveHistory = new List<Move>();
        }
        turn = 1;
        GameRunning = true;
        InputEnabled = true;
        activePlayer = Player.P1;
        ui.SetTurnText(activePlayer);
        ui.GoToGameUI();
        board.CreateBoard(boardSize);
    }
    public void StartGame(int size)
    {
        boardSize = size;
        StartGame();
    }

    [PunRPC] void RPC_GameOver(Player winner, bool tie, int[,] winningTiles)
    {

    }
    public void GameOver(Player winner, bool tie)
    {
        if(tie)
        {
            Debug.Log("Tie Game.");
            ui.ShowTie();
        }
        else
        {
            Debug.Log("Winner: " + winner.ToString());
            //SpewCoins();
            //ShowBigWin();
            ui.ShowWinner(winner);
        }
        InputEnabled = false;
        GameRunning = false;
    }
    public void EndGame()
    {
        GameRunning = false;
        InputEnabled = false;
        board.DestroyBoard();
    }
    public void ResetGame()
    {   
        EndGame();
        StartGame();
    }

    private bool IsMyTurn()
    {
        return activePlayer == localPlayer;
    }
    private int[,] GetTileCoords(List<Tile> tiles)
    {
        int[,] tileCoords = new int[tiles.Count, 2];
        for(int t = 0; t < tiles.Count; t++)
        {
            tileCoords[t, 0] = tiles[t].X;
            tileCoords[t, 1] = tiles[t].Y;
        }
        return tileCoords;
    }
}

public struct Move
{
    public Move(Tile t, Player p)
    {
        turn = 0;
        tile = t;
        player = p;
    }
    public int turn;
    public Tile tile;
    public Player player;
}


/* FOX CUB
    [SerializeField]
    Coin coinPrefab;
    /// <summary>
    /// Coins to spew on each side of the screen
    /// </summary>
    [SerializeField]
    private int coinsToSpew = 20;
    [SerializeField]
    GameObject bigWinPrefab;

    public void SpewCoins()
    {
        Camera cam = Camera.main;
        for(int c = 0; c < coinsToSpew; c++)
        {
            // Spew coin to the left
            Coin lCoin = Instantiate(coinPrefab);
            lCoin.transform.position = cam.ScreenToWorldPoint(new Vector3(cam.pixelWidth, cam.pixelHeight / 2, 5)) + new Vector3(2, 0, 0);
            lCoin.LaunchLeft();
            // Spew coin to the right
            Coin rCoin = Instantiate(coinPrefab);
            rCoin.transform.position = cam.ScreenToWorldPoint(new Vector3(0, cam.pixelHeight / 2, 5)) + new Vector3(-2, 0, 0);
            rCoin.LaunchRight();
        }
    }
    public void ShowBigWin()
    {
        Instantiate(bigWinPrefab);
    }
    */
