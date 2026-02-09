EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100


=== start ===
Yo… you work here?
Listen, man, I can’t hang around long.
I don’t even wanna come in.
Just needed to give you a heads-up.

+ [Who are you?]
    -> who_are_you
+ [A heads-up?]
    -> headsup


=== headsup ===
Yeah. A warning.
My brother’s been acting real strange lately.
Says he’s got some kind of “gig” involving this place.
Tried telling the cops, but they wouldn’t listen. They never take me seriously.
So I figured I’d come myself.

If my brother shows up here to “visit” a grave…
Do not let him in.
You hear me?

+ [Why not?]
    -> why_not
+ [Alright, I understand. Can you give me any more details?]
    -> understood


=== why_not ===
I already told you, man.
He’s been running with the wrong crowd.
Nothing but trouble.

I don’t want him getting in deeper than he already is.
Just turn him away. Don’t let him in, and don’t let him talk you into it.
Him and that weird, cult-like group he’s been hanging around lately…
I don’t know. Something’s off.

+ [Okay. Thanks for the warning.]
    -> okay
+ [Sorry, but I’ll be handling security myself. You should go home.]
    -> declined


=== who_are_you ===
Name’s Reginald. My brother’s Shane.
We’re part of the Deloitte family — live a few miles down the road.

I’m not here to cause trouble.
I just want to stop him from doing something stupid… something he might regret.
He’s always been like this. Always pushing his luck.

So please — can you promise me you won’t let him or his gang in?

+ [Okay. Thanks for the warning.]
    -> okay
+ [Sorry, but I’ll be handling security myself. You should go home.]
~player_scared = player_scared + 1
    -> declined


=== understood ===
Thanks for hearing me out.
We’re the Deloitte family — live not too far from here.

My brother looks a lot like me. Little shorter, but close enough you might mistake us.
He’s been rolling with this strange group lately — all black clothes, weird rituals, causing problems wherever they go.
Real cult-like stuff.

Before he took off, I heard him muttering about this graveyard being “important.”
Didn’t like the sound of that.
Feels like he’s planning to mess with something buried out here.

Just… don’t let him in.
I don’t want to see him ruin his life any more than he already has.
Can you promise me that?

+ [Okay. Thanks for the warning.]
    -> okay
+ [Sorry, but I’ll be handling security myself. You should go home.]
~player_scared = player_scared + 1

    -> declined


=== okay ===
Thanks, man.
I owe you one.
-> END


=== declined ===
Yeah… figures.
Nobody ever takes me seriously.
Hope you all get what’s coming to you.
~GraveyardProtection = GraveyardProtection - 10
-> END
