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
Hey there.
Sorry for disturbing you at this late hour.
My name is Dennis Anderson, and I'm here to visit a family member.
His name is Anthony Anderson.

+ [What is your relation to him?]
    -> relation
+ [Why are you here so late?]
    ~player_stupid = player_stupid + 1
    -> late
    
=== relation ===
Anthony was my brother.
I'll only stop by for a moment.
I try to visit him every year. It's been a while, my visit is a bit overdue.
I hope you don't mind.

+ [Are you here to cause any trouble?]
    ~player_scared = player_scared + 1
    -> trouble
+ [Why are you here so late?]
    ~player_stupid = player_stupid + 1
    -> late
    
=== late ===
Yeah... sorry.
I have a very rigorous work schedule. Terrible hours.
I don't really get the time during the day to make this trip.
I hope it's alright.

-> ask

=== trouble ===
Trouble?
I'm afraid I don't understand.
+ [You seem suspicious.]
    ~player_scared = player_scared + 1
    -> suspicious
+ [Nevermind.]
    ~player_scared = player_scared + 1
    -> ask
    
=== suspicious ===
On second thought...
I'll stop by another time.
Maybe during the day, when there is someone on duty with a bit more manners.
 ~ SetEventVar("allowed_inside", false)
-> END

=== ask ===
So, can I come in?

+ [Alright. Go ahead.]
    -> let_in_response
+ [No. Please leave.]
    -> deny_response
+ [I have more questions.]
-> more_questions
  + [Hold on, I'll be right back.]
      ~ SuspendDialogue()
    -> ask
    
=== ask_name_again ===
I already told you.
Dennis Anderson.
-> more_questions

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
My brother.
Anthony Anderson.
+ [When did he die?]
-> when
+ [What was his cause of death?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [Nevermind.]
-> ask

===when===
Hmm... it's been quite a while.
A little over ten years ago now.
+ [What was his cause of death?]
-> cause_of_death
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
===cause_of_death===
Tragic house fire.
Claimed him and the family. Only the little girl made it out alive.
It was a horrible day.
+ [When did he die?]
-> when
+ [I have other questions.]
-> more_questions
+ [I have no other questions.]
-> ask
=== how_many===
After the event, only the little girl survived.
Her name is Lily.
+ [I have more questions.]
-> more_questions
+ [I have no other questions.]
-> ask



=== let_in_response ===
Thanks.
I won't be long.
 ~ SetEventVar("allowed_inside", true)
-> END

=== deny_response ===
What do you mean "no"?
You don't own those people in there. Those are family members.
I'll just come back during the day, when there's someone more professional on duty.
 ~ SetEventVar("allowed_inside", false)

-> END
