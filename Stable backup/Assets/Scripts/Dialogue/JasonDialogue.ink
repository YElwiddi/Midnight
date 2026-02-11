EXTERNAL SetEventVar(varName, value)
EXTERNAL SuspendDialogue()

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR Sanity = 100

=== start ===
Evening.
Gate still open, huh?
Good.
I need to get inside for a minute.
Just here to visit some graves.

+ [Who are you?]
    -> who_are_you_response
+ [Go on in.]
    ~player_stupid = player_stupid + 2
    -> let_in_response

=== who_are_you_response ===

My name is Jason.
I've been here before many times.

+ [I'll need your full name.]
    -> full_name_response
+ [Alright. Go ahead.]
    ~player_stupid = player_stupid + 1
    -> let_in_response

=== full_name_response ===
Jason Holmes.
I'm here for my brother, Zackary Holmes.
And his son. Edward Holmes.

They were buried close together.
I won't be long.

-> ask

=== ask ===

Can I go in?

+ [Alright. Go ahead.]
    -> let_in_response
+ [Sorry. Graveyard's closed.]
    ~player_mean = player_mean + 2
    -> deny_response
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
My brother and nephew, Zackary and Edward.
+ [When did they die?]
-> when
+ [How did they die?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [Nevermind.]
-> ask

===when===
Last year. They died at the same time.
+ [How did they die?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
===cause_of_death===
They died in a car accident.
+ [When did they die?]
-> when
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
=== how_many===
After their burial, it's just me and my brother's widowed wife.
She disappeared after their deaths.
+[Why?]
-> why_wife
+ [I have more questions.]
-> more_questions
+ [I have no other questions.]
-> ask
=== ask_name_again ===
My name is Jason Holmes.
-> more_questions

===why_wife===
No one knows for certain.
She hasn't spoken to me since their passing.
I can only imagine what she's going through.
+ [I have more questions.]
-> more_questions
+ [I have no other questions.]
-> ask
=== let_in_response ===
Thanks.
I appreciate it.

 ~ SetEventVar("allowed_inside", true)
-> END

=== deny_response ===
…
Yeah.
I should've expected that.
You're going to end up like every other gravekeeper before you.

 ~ SetEventVar("allowed_inside", false)
 ~ Sanity = Sanity - 40
 ~ player_stupid = player_stupid + 1

-> END
