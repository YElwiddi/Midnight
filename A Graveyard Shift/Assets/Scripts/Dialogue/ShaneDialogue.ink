EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100
VAR GraveyardProtection = 100


=== start ===
Hey.
Didn't think anyone would be out here this late.
You work this place?

Listen… I don’t wanna make trouble.
I just need a minute inside.

+ [Who are you?]
    -> who_are_you
+ [It's late. What do you want?]
    -> what_do_you_want


=== who_are_you ===
My name's Shane.
Deloitte family. We live a ways down the road.
I’m not here to steal anything or mess around.

There’s something in there that belongs to my family.
Something that never got handled right.

+ [Handled how?]
    -> handled_how
+ [Rules are rules. I can’t let you in.]
    -> refused_early


=== what_do_you_want ===
I just need to see a grave.
It's unmarked.
Most folks don’t even know it’s there.

I wouldn’t be here if I had another option.

+ [Why is it unmarked?]
    -> why_unmarked
+ [That doesn’t matter. You need to leave.]
    -> refused_early


=== handled_how ===
They buried them in a hurry.
Didn’t ask questions. Didn’t put a name down.
Just wanted it done and forgotten.

That kind of thing sticks with a family.
Sticks with a place too.

+ [You’re talking in circles. Be straight with me.]
    -> be_straight
+ [I’m sorry, but I still can’t let you in.]
    -> refused_early


=== why_unmarked ===
Because it was easier that way.
No paperwork or attention.
Just dirt over a problem.

I know how this sounds.
But I need to see it with my own eyes.
Make sure nothing’s… wrong.

+ [What could be wrong?]
    -> whats_wrong
+ [No. You’re not going inside.]
    -> refused_early


=== be_straight ===
…Alright.
Something’s been off lately.
Bad dreams. Whispers.
Like something’s calling out and not getting an answer.

I just wanna make sure it’s quiet.
That it stays that way.

+ [Fine. You can go in, but be quick.]
    -> let_in
+ [Absolutely not. You’re done here.]
    -> final_refusal


=== whats_wrong ===
The paperwork doesn’t line up.
Dates, plot number... even the depth’s different depending on who you ask.

That usually means someone rushed it, or skipped steps they shouldn’t have.

If something was done wrong, it’s easier to fix it now than after it becomes a bigger problem.
I’ve learned that the hard way.
Will you let me come in and have a look?

+ [Okay. One minute. Then you leave.]
    -> let_in
+ [No.]
    -> final_refusal


=== let_in ===
Thank you.
Really.

I’ll be quick.
~ SetEventVar("allowed_inside", true)
~ GraveRobberSetup = GraveRobberSetup + 1
~GraveyardProtection = GraveyardProtection - 30

-> END


=== refused_early ===
Yeah.
That figures.

Nobody ever wants to deal with things before they rot.
They just lock the gate and walk away.

You have a good night.
~ SetEventVar("allowed_inside", false)
-> END


=== final_refusal ===
…Alright.
I tried to handle this properly.

If this turns into a mess later, just remember I came by.
~ SetEventVar("allowed_inside", false)
-> END
