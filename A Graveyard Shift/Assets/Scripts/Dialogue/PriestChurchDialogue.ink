EXTERNAL SetEventVar(varName, value)
EXTERNAL SetGameBoolFlag(flagName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100

=== start ===
Here we are.
I would like you to enter inside and retrieve it for me. I will wait out here.
It is down the hall. Take the first door to your right.
It is a rather long staircase down to the basement. Just keep going until you reach the bottom.
The rosary should be left in a chest in the back corner.
Before you enter, I would like to give you some advice.
When you go down the hall, do not enter the door on the left. No matter what.
Also, do not listen to any voices that may lead you astray. Stay on your path, and stay resolute.
Do you understand?


+ [Okay. Thank you, father.]
     ~ SetGameBoolFlag("churchdoorunlocked", true)
     ~ SetEventVar("allowed_inside", true)
    -> END
+ [I've changed my mind. You should leave.]
    -> changed_mind
    
    
    === changed_mind ===
    ~Sanity = Sanity - 30
Very well.

I will not force your hand.

May God forgive us both.
 ~ SetEventVar("allowed_inside", false)
-> END