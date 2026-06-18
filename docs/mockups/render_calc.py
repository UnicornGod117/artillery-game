#!/usr/bin/env python3
"""Render of the pop-up scientific calculator keypad (CalculatorView.cs)."""
from PIL import Image, ImageDraw, ImageFont
FONT="/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"; FONTB="/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
_c={}
def f(s,b=False):
    k=(s,b)
    if k not in _c:_c[k]=ImageFont.truetype(FONTB if b else FONT,s)
    return _c[k]
def hx(c):return (int(c[0:2],16),int(c[2:4],16),int(c[4:6],16),255)
P=dict(Bg="1c1812",Panel="0e0c0a",PanelDeep="0a0907",Accent="f4ad3c",AccentDim="b9892f",
    Red="e24a30",Text="ece3d0",TextDim="9a8f78",Faint="6b6151",Border="2c2519",BorderSoft="221d15")
W,H=1440,884
img=Image.new("RGB",(W,H),hx(P["Bg"])[:3]); d=ImageDraw.Draw(img,"RGBA")
# dim backdrop (suggesting the station behind)
d.rectangle([0,0,W,H],fill=hx(P["Bg"]))
d.rectangle([0,0,W,H],fill=(0,0,0,160))
# centered panel
pw,ph=430,560; px=(W-pw)//2; py=(H-ph)//2
def panel(box,bg,bd,bw=1):
    d.rectangle(box,fill=hx(P[bg]))
    if bw:d.rectangle(box,outline=hx(P[bd]),width=bw)
panel([px,py,px+pw,py+ph],"Panel","AccentDim",1)
# header
panel([px,py,px+pw,py+44],"PanelDeep","BorderSoft",0)
d.text((px+14,py+13),"▦  SCIENTIFIC CALCULATOR",font=f(13,True),fill=hx(P["Text"]))
d.text((px+pw-150,py+16),"ARITHMETIC ONLY · deg",font=f(8),fill=hx(P["Faint"]))
d.text((px+pw-58,py+15),"CLOSE ✕",font=f(10),fill=hx(P["TextDim"]))
# display
dx0,dy0,dx1,dy1=px+14,py+56,px+pw-14,py+150
panel([dx0,dy0,dx1,dy1],"PanelDeep","Border",1)
d.text((dx1-10,dy0+8),"sqrt(2*9.817*1240) = 156.06",font=f(10),fill=hx(P["TextDim"]),anchor="ra")
d.text((dx1-10,dy0+24),"570^2 * sin(2*38) / 9.817 = 32112.57",font=f(10),fill=hx(P["TextDim"]),anchor="ra")
# expression line
panel([dx0+8,dy0+42,dx1-8,dy0+74],"Bg","Border",1)
d.text((dx0+16,dy0+48),"570^2 * sin(2*41.5) / 9.817|",font=f(18),fill=hx(P["Text"]))
d.text((dx1-10,dy0+78),"= 32893.18",font=f(15,True),fill=hx(P["Accent"]),anchor="ra")
# keypad 5 cols x 7 rows
keys=[
 [("sin",0),("cos",0),("tan",0),("(",0),(")",0)],
 [("asin",0),("acos",0),("atan",0),("xʸ",0),("√",0)],
 [("ln",0),("log",0),("exp",0),("π",0),("e",0)],
 [("7",1),("8",1),("9",1),("÷",2),("×",2)],
 [("4",1),("5",1),("6",1),("+",2),("−",2)],
 [("1",1),("2",1),("3",1),(".",1),("Ans",0)],
 [("0",1),("00",1),("C",3),("⌫",3),("=",2)],
]
gx0=px+14; gy0=dy0+150; gw=pw-28; gh=H  # compute below
rows=len(keys); cols=5; sp=6
bw=(gw-(cols-1)*sp)/cols; bh=46
for r,row in enumerate(keys):
    for c,(lab,role) in enumerate(row):
        bx=gx0+c*(bw+sp); by=gy0+r*(bh+sp)
        d.rectangle([bx,by,bx+bw,by+bh],outline=hx(P["Border"]),width=1)
        fg={1:P["Text"],2:P["Accent"],3:P["Red"]}.get(role,P["AccentDim"])
        d.text((bx+bw/2,by+bh/2),lab,font=f(16 if role==1 else 13,True),fill=hx(fg),anchor="mm")
# caption strip
d.rectangle([0,H-46,W,H],fill=(12,12,14,255)); d.line([0,H-46,W,H-46],fill=hx(P["Accent"]),width=2)
d.text((16,H-34),"POP-UP SCIENTIFIC CALCULATOR (CalculatorView.cs) — clickable keys + typing, degree-mode trig, history & Ans. "
       "Arithmetic only: it holds no physics, so it can never predict a path or hand over a firing solution.",
       font=f(12),fill=(225,222,214,255))
img.save("/tmp/firing_solution_calculator.png"); print("wrote calculator png")
