EXTERNAL SetEventVar(varName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100

=== start ===
Were you able to retrieve the ring?

+ [Not yet. I'll go get it.]
    ~ SuspendDialogue()
    -> start
+ [I've changed my mind. You should leave.]
    -> changed_mind
    
    
    === changed_mind ===
    ~Sanity = Sanity - 40
Very well.

I will not force your hand.

May God forgive us both.
 ~ SetEventVar("allowed_inside", false)
-> END