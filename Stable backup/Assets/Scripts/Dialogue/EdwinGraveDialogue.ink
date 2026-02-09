EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100


=== start ===
I miss my mother dearly.
She used to hum when she thought no one was listening.
Funny what stays with you, even after everything else is gone.
...
Anyway. I shouldn’t keep you standing here.

+ [Are you going to be okay?]
    -> are_you_okay
+ [Get lost, kid.]
    ~player_mean = player_mean + 2
    ~Sanity = Sanity - 20
    -> leave
    
=== are_you_okay ===
Yeah... I'll get through this.
Thanks for letting me in, I know it's pretty late and you have a job to do.
But this helps more than you know.
    -> END


=== leave ===
...

    -> END