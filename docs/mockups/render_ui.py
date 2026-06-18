#!/usr/bin/env python3
"""
Faithful render of the FIRING SOLUTION Godot shell, reconstructed from the C#
source (Godot can't run headless in this container). Layout, palette and text
mirror StationView/KineticStation/BeamStation; numbers are the REAL Core mission
data for the two default seeds. Red callouts flag design/visual issues.
"""
from PIL import Image, ImageDraw, ImageFont
import math

FONT = "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"
FONTB = "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"
_cache = {}
def f(size, bold=False):
    k = (size, bold)
    if k not in _cache:
        _cache[k] = ImageFont.truetype(FONTB if bold else FONT, size)
    return _cache[k]

def hx(c, a=255):
    return (int(c[0:2],16), int(c[2:4],16), int(c[4:6],16), a)

AMBER = dict(Bg="1c1812",Panel="0e0c0a",PanelDeep="0a0907",Accent="f4ad3c",AccentDim="b9892f",
    Red="e24a30",Text="ece3d0",TextDim="9a8f78",Faint="6b6151",Border="2c2519",BorderSoft="221d15")
ICE = dict(Bg="161e26",Panel="0b0f14",PanelDeep="080c10",Accent="3cc6e8",AccentDim="5fb8d0",
    Red="ff5a47",Text="dce8f2",TextDim="8195a3",Faint="5d6b78",Border="243039",BorderSoft="16202a")

W, H = 1440, 884
LEGEND = 250

def text(d,p,xy,s,col,size,bold=False,anchor="la"):
    d.text(xy, s, font=f(size,bold), fill=hx(p[col]), anchor=anchor)

def panel(d,p,box,bg="PanelDeep",border="BorderSoft",bw=1):
    x0,y0,x1,y1=box
    d.rectangle(box, fill=hx(p[bg]))
    if bw: d.rectangle(box, outline=hx(p[border]), width=bw)

def section(d,p,x,y,w,cap,bullet="Accent",tag=None):
    d.rectangle([x,y+5,x+5,y+10], fill=hx(p[bullet]))
    text(d,p,(x+12,y),cap.upper(),"TextDim",11)
    tw=d.textlength(cap.upper(),font=f(11))
    d.line([x+12+tw+8,y+7,x+w-(40 if tag else 4),y+7], fill=hx(p["BorderSoft"]),width=1)
    if tag: text(d,p,(x+w,y),tag,"Faint",8,anchor="ra")
    return y+22

def metric_cell(d,p,box,cap,val,valcol,vsize=15):
    panel(d,p,box,"PanelDeep","BorderSoft",1)
    x0,y0=box[0],box[1]
    text(d,p,(x0+9,y0+8),cap,"Faint",9)
    d.text((x0+9,y0+22),val,font=f(vsize),fill=valcol)

def field(d,p,box,label,value):
    x0,y0,x1,y1=box
    text(d,p,(x0,y0),label,"Faint",9)
    fb=[x0,y0+14,x1-32,y0+50]
    panel(d,p,fb,"PanelDeep","Border",1)
    d.line([fb[0],fb[3],fb[2],fb[3]],fill=hx(p["Accent"]),width=2)
    d.text((x0+9,y0+22),value,font=f(20),fill=hx(p["Accent"]))
    # steppers
    for i,ch in enumerate(("▲","▼")):
        sb=[x1-28,y0+14+i*18,x1,y0+32+i*18]
        d.rectangle(sb,outline=hx(p["Border"]),width=1)
        text(d,p,((sb[0]+sb[2])/2,(sb[1]+sb[3])/2-5),ch,"AccentDim",9,anchor="ma")

def callout(d,p,n,xy,col=(225,40,40,255)):
    x,y=xy
    d.ellipse([x-11,y-11,x+11,y+11],fill=col,outline=(255,255,255,255),width=2)
    txt = n if isinstance(n,str) else str(n)
    d.text((x,y),txt,font=f(12,True),fill=(255,255,255,255),anchor="mm")

# ----------------------------------------------------------------------------
def render(kind, data, issues, callouts, outfile):
    p = AMBER if kind=="kin" else ICE
    img = Image.new("RGB",(W,H+LEGEND),hx(p["Bg"])[:3])
    d = ImageDraw.Draw(img,"RGBA")
    d.rectangle([0,0,W,H],fill=hx(p["Bg"]))

    # ---- top bar (h 46) ----
    panel(d,p,[0,0,W,46],"PanelDeep","Border",0)
    d.polygon([(28,17),(35,10),(42,17),(35,24)],fill=hx(p["Accent"]))
    text(d,p,(52,14),"FIRING SOLUTION","Text",16,True)
    text(d,p,(210,17),data["subtitle"],"Faint",10)
    cx=560
    for ctext,hi in data["chips"]:
        tw=d.textlength(ctext,font=f(10))
        cb=[cx,12,cx+tw+14,34]
        panel(d,p,cb,"Bg" if hi else "Panel","AccentDim" if hi else "Border",1)
        text(d,p,(cx+7,16),ctext,"Accent" if hi else "TextDim",10)
        cx+=tw+24
    # reload bar
    text(d,p,(W-470,10),data["reload"],"Faint",8)
    panel(d,p,[W-470,22,W-342,27],"PanelDeep","Border",1)
    d.rectangle([W-470,22,W-470+int(128*0.73),27],fill=hx(p["Accent"]))
    # NEW MISSION button
    nm=[W-322,11,W-190,35]
    d.rectangle(nm,outline=hx(p["AccentDim"]),width=1)
    text(d,p,((nm[0]+nm[2])/2,18),"↻ NEW MISSION","AccentDim",10,anchor="ma")
    text(d,p,(W-16,14),"CAREER 13,690 PTS","TextDim",11,anchor="ra")

    midy=47
    # ---- LEFT PANEL (0..312) ----
    LX,LW=0,312
    panel(d,p,[LX,midy,LX+LW,H],"Panel","Border",0)
    x=LX+16; y=midy+16
    y=section(d,p,x,y,LW-32,"Environment",tag="MEASURED")+8
    # compass
    cc=(x+39,y+39); r=34
    d.ellipse([cc[0]-r,cc[1]-r,cc[0]+r,cc[1]+r],outline=hx(p["Border"]),width=1)
    d.ellipse([cc[0]-r*.74,cc[1]-r*.74,cc[0]+r*.74,cc[1]+r*.74],outline=hx(p["BorderSoft"]),width=1)
    a=math.radians(data["wfrom"]); dirx,diry=math.sin(a),-math.cos(a)
    d.line([cc[0]-dirx*r*.5,cc[1]-diry*r*.5,cc[0]+dirx*r*.5,cc[1]+diry*r*.5],fill=hx(p["Accent"]),width=2)
    text(d,p,(cc[0]-3,cc[1]-r+4),"N","TextDim",7)
    wx=x+90
    text(d,p,(wx,y+6),data["windcap"],"Faint",9)
    d.text((wx,y+18),data["wind"],font=f(21),fill=hx(p["Text"]))
    text(d,p,(wx,y+46),data["windfrom"],"AccentDim",11)
    y+=86
    cells=data["env"]
    for i,(cap,val) in enumerate(cells):
        bx=LX+16+(i%2)*((LW-32)//2+0); bw=(LW-33)//2
        box=[bx if i%2==0 else LX+16+bw+1, y+(i//2)*47, (bx+bw) if i%2==0 else LX+16+2*bw+1, y+(i//2)*47+44]
        metric_cell(d,p,box,cap,val,hx(p["Text"]))
    y+=2*47+10
    y=section(d,p,x,y,LW-32,"Target — Observed","Red",data["spotter"])+6
    for i,(cap,val) in enumerate(data["tgt"]):
        bw=(LW-33)//2
        box=[LX+16+(i%2)*(bw+1), y+(i//2)*47, LX+16+(i%2)*(bw+1)+bw, y+(i//2)*47+44]
        metric_cell(d,p,box,cap,val,hx("e9ddc6") if kind=="kin" else hx(p["Text"]))
    y+=2*47+6
    text(d,p,(x,y),data["loc"],"Faint",9); y+=22
    y=section(d,p,x,y,LW-32,"Weapon Configuration")+6
    panel(d,p,[x,y,x+LW-32,y+30],"PanelDeep","Border",1)
    text(d,p,(x+9,y+8),data["weapon"],"Text",12)
    text(d,p,(x+LW-44,y+9),"▾","Faint",9)
    y+=38
    for i,(cap,val) in enumerate(data["wcfg"]):
        bw=(LW-33)//2
        box=[LX+16+i*(bw+1), y, LX+16+i*(bw+1)+bw, y+40]
        metric_cell(d,p,box,cap,val,hx("cdbf9f") if kind=="kin" else hx("aebecb"),13)

    # ---- RIGHT PANEL (1068..1440) ----
    RX,RW=1068,372
    panel(d,p,[RX,midy,W,H],"Panel","Border",0)
    x=RX+15; y=midy+15
    y=section(d,p,x,y,RW-30,"Firing Solution — Your Input")+4
    text(d,p,(x,y),"↳ type, or use the steppers. Nothing here is computed for you.","Faint",9); y+=15
    text(d,p,(x,y),data["prec1"],"AccentDim",9); y+=14
    text(d,p,(x,y),data["prec2"],"Faint",8); y+=18
    fb_w=(RW-30-11)//2
    for i,(lab,val) in enumerate(data["fields"]):
        fx=x+(i%2)*(fb_w+11); fy=y+(i//2)*64
        field(d,p,[fx,fy,fx+fb_w,fy+50],lab,val)
    y+=2*64+4
    if kind=="kin":
        # charge pips
        pw=(RW-30-6*5)/7
        for i in range(7):
            px=x+i*(pw+5)
            d.rectangle([px,y,px+pw,y+15],fill=hx(p["Accent"]) if i<5 else hx(p["BorderSoft"]))
        y+=22
        text(d,p,(x,y),"MUZZLE VELOCITY v₀ (from charge)","TextDim",10)
        text(d,p,(x+RW-30,y),"570 m/s","Text",10,anchor="ra"); y+=20
    else:
        # relativistic regime
        panel(d,p,[x,y,x+RW-30,y+22],"Bg","BorderSoft",0)
        text(d,p,(x+9,y+6),"RELATIVISTIC REGIME","TextDim",10)
        text(d,p,(x+RW-39,y+7),"β,γ FIXED · SET PULSE ENERGY","AccentDim",8,anchor="ra"); y+=23
        reg=[("β","0.940"),("LORENTZ γ","2.931"),("PULSE ENERGY","4.2 GJ"),("KILL THRESHOLD","≥ 4.3 GJ")]
        bw=(RW-30)//2
        for i,(cap,val) in enumerate(reg):
            box=[x+(i%2)*bw, y+(i//2)*42, x+(i%2)*bw+bw-1, y+(i//2)*42+40]
            panel(d,p,box,"PanelDeep","BorderSoft",0)
            text(d,p,(box[0]+9,box[1]+7),cap,"Faint",8)
            col = p["Accent"] if cap=="PULSE ENERGY" else ("aebecb" if cap=="KILL THRESHOLD" else p["Text"])
            d.text((box[0]+9,box[1]+18),val,font=f(14),fill=hx(col) if cap!="KILL THRESHOLD" else hx("aebecb"))
        y+=2*42+8
    # commit button
    panel(d,p,[x,y,x+RW-30,y+44],"Panel","Accent",0)
    d.rectangle([x,y,x+RW-30,y+44],fill=hx(p["Accent"]))
    d.text(((x+x+RW-30)/2,y+22),"◆  COMMIT & FIRE",font=f(16,True),fill=hx(p["Bg"]),anchor="mm")
    y+=54
    # calculator — opens the pop-up keypad
    d.rectangle([x,y,x+RW-30,y+30],outline=hx(p["Border"]),width=1)
    text(d,p,(x+9,y+9),"▦  SCIENTIFIC CALCULATOR","Accent",10)
    text(d,p,(x+RW-39,y+9),"· keypad","Faint",10,anchor="ra")
    y+=38
    # handbook — now opens the Core formula reference
    d.rectangle([x,y,x+RW-30,y+30],outline=hx(p["Border"]),width=1)
    text(d,p,(x+9,y+9),"▤  HANDBOOK","Accent",10)
    text(d,p,(x+112,y+9),data["hbk"],"Faint",10)
    y+=38
    half=(RW-30-9)//2
    for i,(lab,col) in enumerate([("HELP","AccentDim"),("GIVE UP","Faint")]):
        bx=x+i*(half+9)
        d.rectangle([bx,y,bx+half,y+34],outline=hx(p["Border"]),width=1)
        text(d,p,((bx+bx+half)/2,y+17),lab,col,10,anchor="mm")

    # ---- CENTER: plotting board + vertical plane ----
    CX0,CX1=313,1067
    bdy0,bdy1=midy,H-215
    panel(d,p,[CX0,bdy0,CX1,bdy0+34],"PanelDeep","BorderSoft",0)
    d.rectangle([CX0+8,bdy0+14,CX0+13,bdy0+19],fill=hx(p["Accent"]))
    text(d,p,(CX0+20,bdy0+9),"PLOTTING BOARD","Text",12)
    # measurement toolbar
    tx=CX0+150
    for lab,active,dim in [("PAN",True,False),("RULER",False,False),("ANGLE",False,False),
                           ("PEN",False,False),("GRID",False,True),("CLR",False,True)]:
        tw=d.textlength(lab,font=f(10))+14
        d.rectangle([tx,bdy0+6,tx+tw,bdy0+28],outline=hx(p["Accent"] if active else p["Border"]),width=1)
        d.text((tx+tw/2,bdy0+11),lab,font=f(10),fill=hx(p["Accent"] if active else (p["Faint"] if dim else p["AccentDim"])),anchor="ma")
        tx+=tw+5
    for i,ch in enumerate(("+","−","⌖")):
        bx=CX1-30-(2-i)*30
        d.rectangle([bx,bdy0+6,bx+24,bdy0+28],outline=hx(p["Border"]),width=1)
        text(d,p,(bx+12,bdy0+11),ch,"Text" if i<2 else "TextDim",13,anchor="ma")
    # board area
    panel(d,p,[CX0,bdy0+35,CX1,bdy1],"PanelDeep","PanelDeep",0)
    # faint cartesian GRID overlay (toggled on)
    gun_pre=(CX0+0.20*(CX1-CX0), bdy0+35+0.82*(bdy1-bdy0-35)); gstep=2000*(0.038 if kind=="kin" else 0.0098)
    for k in range(-24,25):
        gx=gun_pre[0]+k*gstep
        if CX0<gx<CX1: d.line([gx,bdy0+35,gx,bdy1],fill=hx(p["BorderSoft"]),width=1)
        gy=gun_pre[1]+k*gstep
        if bdy0+35<gy<bdy1: d.line([CX0,gy,CX1,gy],fill=hx(p["BorderSoft"]),width=1)
    gun=(CX0+0.20*(CX1-CX0), bdy0+35+0.82*(bdy1-bdy0-35))
    ppm=0.038 if kind=="kin" else 0.0098
    ringstep=2000 if kind=="kin" else 10000
    for i in range(1,5):
        rr=i*ringstep*ppm
        d.ellipse([gun[0]-rr,gun[1]-rr,gun[0]+rr,gun[1]+rr],outline=hx(p["Border"]),width=1)
        text(d,p,(gun[0]+rr+3,gun[1]-8),f"{i*ringstep//1000}km","Faint",8)
    d.line([gun[0]-8,gun[1],gun[0]+8,gun[1]],fill=hx(p["AccentDim"]),width=1)
    d.line([gun[0],gun[1]-8,gun[0],gun[1]+8],fill=hx(p["AccentDim"]),width=1)
    d.ellipse([gun[0]-3,gun[1]-3,gun[0]+3,gun[1]+3],outline=hx(p["Accent"]),width=2)
    text(d,p,(gun[0]-18,gun[1]+22),"EMITTER" if kind=="beam" else "GUN FCS-01","AccentDim",8)
    brg=math.radians(data["tbrg"]); tr=data["trange"]
    tp=(gun[0]+tr*math.sin(brg)*ppm, gun[1]-tr*math.cos(brg)*ppm)
    # aim line (dashed)
    ab=math.radians(data["aim"]); adir=(math.sin(ab),-math.cos(ab)); aend=(gun[0]+adir[0]*58, gun[1]+adir[1]*58)
    dash_line(d,gun,aend,hx(p["Accent"]),1,6,4)
    text(d,p,(aend[0]-12,aend[1]-14),f"BRG {data['aim']:.1f}°","Accent",8)
    # target mark
    d.rectangle([tp[0]-6,tp[1]-6,tp[0]+6,tp[1]+6],outline=hx(p["Red"]),width=2)
    d.line([tp[0]-11,tp[1],tp[0]+11,tp[1]],fill=hx(p["Red"]),width=1)
    d.line([tp[0],tp[1]-11,tp[0],tp[1]+11],fill=hx(p["Red"]),width=1)
    text(d,p,(tp[0]+10,tp[1]-2),data["tgtlabel"],"Red",8)
    text(d,p,(CX1-40,bdy0+70),"N","TextDim",9)
    d.line([CX1-36,bdy0+95,CX1-36,bdy0+62],fill=hx(p["TextDim"]),width=1)
    # sample RULER measurement (triangulation aid)
    ra=(gun[0]+120,gun[1]-40); rb=(gun[0]+300,gun[1]-150)
    d.line([ra[0],ra[1],rb[0],rb[1]],fill=hx(p["Accent"]),width=1)
    for pt in (ra,rb): d.ellipse([pt[0]-3,pt[1]-3,pt[0]+3,pt[1]+3],outline=hx(p["Accent"]),width=1)
    distm=((rb[0]-ra[0])**2+(rb[1]-ra[1])**2)**0.5/ppm
    import math as _m; brgm=( _m.degrees(_m.atan2(rb[0]-ra[0],-(rb[1]-ra[1]))) )%360
    text(d,p,((ra[0]+rb[0])/2+6,(ra[1]+rb[1])/2-6),f"{distm/1000:.2f} km · {brgm:.1f}°","Text",9)
    text(d,p,(CX0+10,bdy0+51),"RULER · click 2 points · right-click cancels","TextDim",8)

    # vertical plane
    vy0=H-214
    panel(d,p,[CX0,vy0,CX1,vy0+28],"PanelDeep","BorderSoft",0)
    d.rectangle([CX0+8,vy0+11,CX0+13,vy0+16],fill=hx(p["Accent"]))
    text(d,p,(CX0+20,vy0+7),"VERTICAL PLANE — RANGE / ALTITUDE","Text",11)
    vpx1=CX1-185
    panel(d,p,[CX0,vy0+29,vpx1,H],"PanelDeep","PanelDeep",0)
    left=CX0+40; bottom=H-22; topp=vy0+43; plotw=vpx1-left-16; ploth=bottom-topp
    d.line([left,bottom,left+plotw,bottom],fill=hx(p["Border"]),width=1)
    d.line([left,bottom,left,topp],fill=hx(p["Border"]),width=1)
    text(d,p,(CX0+14,topp+26),"ALT","Faint",8)
    for i in range(1,5):
        gx=left+plotw*i/4
        d.line([gx,topp+6,gx,bottom],fill=hx(p["BorderSoft"]),width=1)
    # altitude window with gun level (0) drawn — target may sit BELOW the gun
    vmin,vmax,tgtalt=data["vmin"],data["vmax"],data["tgtalt"]
    def yA(a): return bottom-(a-vmin)/(vmax-vmin)*ploth
    gunY=yA(0); g0=(left,gunY)
    # gun-level / horizon reference line
    dash_line(d,(left,gunY),(left+plotw,gunY),hx(p["BorderSoft"]),1,6,5)
    text(d,p,(left+plotw-70,gunY-12),"GUN LEVEL 0 m","Faint",8)
    if vmin<0: text(d,p,(CX0+8,bottom-12),f"{vmin:.0f} m","Faint",8)
    text(d,p,(CX0+8,topp+2),f"+{vmax/1000:.1f}km","Faint",8)
    d.ellipse([g0[0]-3,g0[1]-3,g0[0]+3,g0[1]+3],outline=hx(p["Accent"]),width=2)
    el=math.radians(data["el"])
    d.line([g0[0],g0[1],g0[0]+math.cos(el)*110,g0[1]-math.sin(el)*110],fill=hx(p["Accent"]),width=2)
    text(d,p,(g0[0]+40,g0[1]-14),f"LAY {data['el']:.1f}°","Accent",9)
    tgx=left+0.62*plotw; tgy=yA(tgtalt)
    dash_line(d,(tgx,gunY),(tgx,tgy),hx(p["Red"]),1,3,3)
    d.rectangle([tgx-5,tgy-5,tgx+5,tgy+5],outline=hx(p["Red"]),width=2)
    altlab=f"+{tgtalt:.0f} m" if tgtalt>=0 else f"{tgtalt:.0f} m"
    text(d,p,(tgx-34,tgy+(22 if tgtalt<0 else -10)),f"TGT {tr/1000:.1f}km · {altlab}","Red",8)
    # last-shot side panel
    panel(d,p,[vpx1+1,vy0+29,CX1,H],"PanelDeep","BorderSoft",0)
    text(d,p,(vpx1+13,vy0+41),"LAST SHOT — OBSERVED","Faint",8)
    text(d,p,(vpx1+13,vy0+58),"— NO SHOT FIRED —","Faint",10)

    GREEN=(70,190,110,255)
    # ---- callouts ----
    for n,xy in callouts:
        callout(d,p,"✓",xy,GREEN)

    # ---- legend strip ----
    ly=H+12
    d.rectangle([0,H,W,H+LEGEND],fill=(12,12,14,255))
    d.line([0,H,W,H],fill=GREEN,width=2)
    d.text((16,ly),data["legendtitle"],font=f(15,True),fill=GREEN)
    ly+=26
    colw=W//2
    for i,(n,txt) in enumerate(issues):
        col=i//6; row=i%6
        ex=16+col*colw; ey=ly+row*34
        callout(d,p,"✓",(ex+11,ey+9),GREEN)
        d.text((ex+28,ey),txt,font=f(12),fill=(225,222,214,255))
    img.save(outfile)
    print("wrote",outfile)

def dash_line(d,a,b,col,w,dash=7,gap=5):
    dx,dy=b[0]-a[0],b[1]-a[1]; ln=math.hypot(dx,dy)
    if ln<1: return
    ux,uy=dx/ln,dy/ln; pos=0
    while pos<ln:
        e=min(pos+dash,ln)
        d.line([a[0]+ux*pos,a[1]+uy*pos,a[0]+ux*e,a[1]+uy*e],fill=col,width=w)
        pos=e+gap

# ============================ DATA ============================
kin = dict(
    subtitle="FCS-01 · STATION ALPHA · Kinetic artillery",
    chips=[("WPN · KINETIC ARTILLERY",True),("WORLD · EARTH",False),("TIER · MEDIUM I",False),("MSN-4471",False)],
    reload="RELOAD CYCLE", wfrom=42, wind="0.0 m/s", windfrom="FROM 042°", windcap="WIND VECTOR",
    env=[("ALTITUDE","828 m"),("AIR TEMP","12.5 °C"),("AIR DENSITY ρ","1.111 kg/m³"),("LOCAL g","9.817 m/s²")],
    spotter="SPOTTER",
    tgt=[("GROUND RANGE · 0.01 km","7.49 km"),("BEARING · 0.1°","335.4 °"),("TGT ALTITUDE · 1 m","-98 m"),("MOTION","STATIC")],
    loc="↳ Localised from OP-1 / OP-2. Range & drop are yours to solve.",
    weapon="M-72 HE-FRAG", wcfg=[("SHELL MASS","43.2 kg"),("BALLISTIC COEF","0.255")],
    prec1="↳ required precision: azimuth & elevation to 0.1°, charge is an integer 1–7.",
    prec2="   near a charge's max range the arc is steep — type elevation for full 0.1° control.",
    fields=[("AZIMUTH (x) · ° · ±0.1°","335.0"),("ELEVATION (y) · ° · ±0.1°","45.0"),
            ("Z-CORR (cross) · ° · ±0.1°","0.0"),("PROPELLANT CHARGE · 1–7","5")],
    trange=7494, tbrg=335.4, aim=335.0, el=45.0, tgtlabel="TGT · ARMOR · 7.49km",
    tgtalt=-98, vmin=-300, vmax=4000,
    calchist="570^2 * sin(2*45) / 9.817  =  33106.85",
    calcinput="sqrt(2*9.817*1240)|", hbk=" · Ballistics / Trig / Relativity",
    legendtitle="KINETIC STATION — implemented this session  (faithful reconstruction from the C# shell source)",
)
kin_issues=[
 (1,"NEW MISSION button generates a fresh procedural target in place."),
 (1,"Career score now persists (user://career.save) and updates live on a kill."),
 (1,"Scientific calculator is a real evaluator: + − × ÷ ^ ( ), sin/cos/sqrt…, degree-mode."),
 (1,"HANDBOOK button opens the Core formula reference; HELP gives the tier-aware hint."),
 (1,"Input precision is stated on every field (az/el 0.1°, charge 1–7)."),
 (1,"Give-up always reveals a working solution (azimuth lead + coarse-to-fine)."),
 (1,"Plotting board now has measurement tools: GRID, RULER (dist+bearing), PROTRACTOR, PEN."),
 (1,"Vertical plane shows target altitude vs the gun — above OR below gun level."),
]
kin_callouts=[(1,(1240,58)),(1,(1290,20)),(1,(1100,400)),(1,(1100,471))]

beam = dict(
    subtitle="DEW-02 · STATION BETA · Relativistic beam",
    chips=[("WPN · RELATIVISTIC BEAM",True),("WORLD · KEPLER-9c",False),("TIER · HARD",False),("MSN-9120",False)],
    reload="CAPACITOR CHARGE", wfrom=255.9, wind="179 m/s", windfrom="BEARING 255.9°", windcap="CLOSING VELOCITY",
    env=[("TGT ALTITUDE","7.4 km"),("AIR TEMP","-27 °C"),("AIR DENSITY ρ","0.155 kg/m³"),("LOCAL g","13.87 m/s²")],
    spotter="SENSOR",
    tgt=[("SLANT RANGE · 0.1 km","31.1 km"),("BEARING · 0.1°","255.9 °"),("LOS ELEVATION · 0.1°","13.8 °"),("CLOSING · 1 m/s","179 m/s")],
    loc="↳ Near-c flight makes lead negligible — the work is energy & γ.",
    weapon="PROTON · FOCUSED", wcfg=[("REST MASS m₀","938 MeV/c²"),("BEAM β","0.940 c")],
    prec1="↳ required precision: point within on-axis tolerance; deliver E ≥ kill threshold (0.1 GJ).",
    prec2="",
    fields=[("AZIMUTH (x) · ° · ±0.1°","256.0"),("ELEVATION (y) · ° · ±0.1°","14.0"),
            ("Z-CORR (cross) · ° · ±0.1°","0.0"),("PULSE ENERGY · GJ · ±0.1","4.2")],
    trange=31063, tbrg=255.9, aim=256.0, el=14.0, tgtlabel="TGT · AIRCRAFT · 31.1km",
    tgtalt=7400, vmin=0, vmax=12000,
    calchist="31063 / (0.94 * 299792458)  =  0.000110",
    calcinput="(2.931 - 1) * 938 * 1.602e-19|", hbk=" · Relativity / Thermal / Vectors",
    legendtitle="BEAM STATION — implemented this session  (faithful reconstruction from the C# shell source)",
)
beam_issues=[
 (1,"Environment now reads the REAL mission: closing velocity, target bearing,"),
 (1,"   target altitude from the LOS triangle, air temp/density, local g (KEPLER-9c)."),
 (1,"Regime panel relabelled honestly: \"β,γ FIXED · SET PULSE ENERGY\" (no false derivation)."),
 (1,"Calculator + handbook + tier-aware HELP now functional, as on the kinetic station."),
 (1,"NEW MISSION + persisted career are shared station chrome."),
 (1,"Board measurement tools (grid/ruler/protractor/pen) on both stations."),
 (1,"Vertical plane plots target altitude relative to the gun."),
]
beam_callouts=[(1,(55,130)),(1,(170,198)),(1,(1245,278)),(1,(1100,475)),(1,(1240,58))]

render("kin",kin,kin_issues,kin_callouts,"/tmp/firing_solution_kinetic.png")
render("beam",beam,beam_issues,beam_callouts,"/tmp/firing_solution_beam.png")
