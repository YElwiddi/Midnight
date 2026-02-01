EXTERNAL SetEventVar(varName, value)

VAR player_mean = 0
VAR player_scared = 0
VAR player_stupid = 0
VAR SpiritAngered = 0
VAR GraveRobberSetup = 0
VAR GraveyardProtection = 100
VAR Sanity = 100

=== start ===
Um...
H-hello?
You work here, right?
I think you do.
I mean, you're standing there like you do.
So.
I was wondering if I could come in for a minute.
Just a minute.
Something feels off behind me.
But it might just be the wind.

+ [What do you mean “off”?]
    -> explain_fear
+ [What did you see?]
    -> explain_fear

=== explain_fear ===
R-right.
So when I was walking, I heard footsteps.
I think.
When I stopped walking, the footsteps stopped.
Which is normal.
Unless someone else stopped at the same time.
That happens in movies.

+ [Movies aren’t real. Did you see anyone?]
    -> saw_anyone
+ [You’re shaking. Take a breath.]
    ~player_scared = player_scared + 1
    -> calm_response

=== doubt_response ===
Y-yeah.
That's what my mom used to say.
That I think too much.
Or not enough.
I never figured out which one she meant.
I'm probably just tired.
But being tired makes it easier for bad things to happen, I think.
Or maybe that's naps.
I get those mixed up.

+ [Focus. Did you actually see someone?]
    -> saw_anyone
+ [Go home. You’re fine.]
    ~player_mean = player_mean + 2
    -> send_away

=== calm_response ===
Oh.
Okay.
Breathing.
In.
Out.
I think I'm doing it wrong.
But I feel a little better.
Still don't like the dark though.
The dark feels like it's leaning.
Does it look like it's leaning to you?

+ [No. Tell me what you saw.]
    -> saw_anyone
+ [You’re imagining things.]
    -> doubt_response

=== saw_anyone ===
S-see them?
Not clearly.
There was a shape.
Or maybe a space where a shape should've been.
It was near the trees.
But trees are very suspicious at night.
They move even when they shouldn't.
This one felt like it noticed me noticing it.
Which sounds silly.
I know that.

+ [Describe the shape.]
    -> describe_shape
+ [You’re just scared.]
    ~player_scared = player_scared + 1
    -> maybe_response
    
    === maybe_response===
Oh...
M-maybe you're right.
-> ask

=== ask ===
I'd feel a lot better if I could come inside for a moment...
+ [You can come inside. Quickly.]
    -> let_inside
+ [You should leave right now.]
    -> send_away
+ [What is your name?]
    -> ask_name_again
+ [Hold on, I'll be right back.]
    -> deny_brb


=== ask_name_again ===
My name is Jessica.
-> ask

=== deny_brb ===
Um...
Not to be rude, but I don't think I would feel comfortable standing out here by myself.
-> ask

=== describe_shape ===
Um.
It was tall.
Or average.
Taller than me.
That part I’m sure about.
It didn't move much.
Which was the worst part.
If it moved, I'd know it was real.
I think it had something dark on.
Like a coat.
Or a shadow pretending to be a coat.
-> ask



=== let_inside ===
R-really?
Thank you.
I promise I won't touch anything.
Or look at anything too long.

~GraveyardProtection = GraveyardProtection - 20

~ SetEventVar("allowed_inside", true)

-> END

=== send_away ===
Oh.
Okay.
That's fair.
You're probably right.
I'm sorry for bothering you.
I'll just walk fast.
Fast walking makes you harder to catch.
I heard that somewhere.
If I start running, don't worry.
That's normal for me.


~ SetEventVar("allowed_inside", false)
~Sanity = Sanity - 25
-> END
