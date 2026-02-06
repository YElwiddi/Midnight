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
I haven't been able to sleep right. The guilt from the quick burial...
I just wanna make sure the plot is alright.

-> ask


=== whats_wrong ===
The paperwork doesn’t line up.
Dates, plot number... even the depth’s different depending on who you ask.

That usually means someone rushed it, or skipped steps they shouldn’t have.

If something was done wrong, it’s easier to fix it now than after it becomes a bigger problem.
I’ve learned that the hard way.

-> ask

=== ask ===
Will you let me come in and have a look?

+  [Fine. You can go in, but be quick.]
    -> let_in
+ [No.]
    -> final_refusal
+ [I have a few more questions.]
    -> more_questions
  + [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> ask
    
        
    
=== more_questions ===
+ [What was your name again?]
    -> ask_name_again
+ [Who are you visiting?]
    -> who_visit
+ [How many people are in your family?]
    -> how_many
+ [Nevermind.]
-> ask

=== who_visit ===
My brother, Reginald.
+ [When did he die?]
-> when
+ [What was his cause of death?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [Nevermind.]
-> ask

===when===
It was very recent.
A little over a week ago.
You weren't working here when it happened.
+ [What was his cause of death?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
===cause_of_death===
He died close to here.
He was in a terrible shape when they found him. I don't want to recall the details.
I don't know for certain.
+ [When did he die?]
-> when
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
=== how_many===
After my brother's death, it's just me.
+ [I have more questions.]
-> more_questions
+ [I have no other questions.]
-> ask

=== ask_name_again ===
My name?
Shane Deloitte.
-> more_questions

=== let_in ===
Thank you.
Really.

I’ll be quick.
~ SetEventVar("allowed_inside", true)
~GraveyardProtection = GraveyardProtection - 50

-> END


=== refused_early ===
Yeah.
That figures.

Nobody ever wants to deal with things before they rot.
They just lock the gate and walk away.

If this turns into a mess later, just remember I came by.

~ SetEventVar("allowed_inside", false)
-> END


=== final_refusal ===
…Alright.
I tried to handle this properly.

If this turns into a mess later, just remember I came by.
~ SetEventVar("allowed_inside", false)
-> END
