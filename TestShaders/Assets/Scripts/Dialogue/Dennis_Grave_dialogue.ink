EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100


=== start ===
It's a damn shame what happened that day.
Wish I could've been there.

+ [Where were you when it happened?]
    -> where
+ [I think you should be going now.]
    ~player_mean = player_mean + 1

    -> leave
    
=== where ===
I...
I think I should be going now. Have a great rest of your evening.
    ~Sanity = Sanity - 45

    -> END
    
=== leave ===
Yeah, you're probably right.
-> bye_text



=== bye_text ===
I'll be heading out now.
    ~Sanity = Sanity - 45

    -> END