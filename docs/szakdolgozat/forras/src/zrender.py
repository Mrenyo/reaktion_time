import sys, os, struct, numpy as np
from PIL import Image, ImageFilter
S=sys.argv[1]
d=r"C:\Users\mrenk\OneDrive\Dokumentumok\Egyéni Office-sablonok\TMDK\3D modellek"
def load(p):
    b=open(p,"rb").read()
    if b[:5]==b"solid" and b"facet" in b[:2000]:
        v=[[float(x) for x in l.split()[1:4]] for l in b.decode("ascii","ignore").splitlines() if l.strip().startswith("vertex")]
        return np.array(v).reshape(-1,3,3)
    n=struct.unpack("<I",b[80:84])[0]
    a=np.frombuffer(b,dtype=np.dtype([("n","<f4",3),("v","<f4",(3,3)),("a","<u2")]),count=n,offset=84)
    return a["v"].astype(float)
def rot(az,el):
    a=np.radians(az); e=np.radians(el)
    Rz=np.array([[np.cos(a),-np.sin(a),0],[np.sin(a),np.cos(a),0],[0,0,1]])
    Rx=np.array([[1,0,0],[0,np.cos(e),-np.sin(e)],[0,np.sin(e),np.cos(e)]])
    return Rx@Rz
def render(tri,out,az=35,el=-62,W=1000,ss=2):
    R=rot(az,el); t=tri@R.T   # camera looks along -z; x right, y up
    p=t.reshape(-1,3); mn=p.min(0); mx=p.max(0)
    Wp=W*ss; pad=0.06
    sc=Wp*(1-2*pad)/max(mx[0]-mn[0],mx[1]-mn[1])
    Hp=int((mx[1]-mn[1])*sc+2*pad*Wp); Wq=int((mx[0]-mn[0])*sc+2*pad*Wp)
    X=(t[:,:,0]-mn[0])*sc+pad*Wp; Y=Hp-((t[:,:,1]-mn[1])*sc+pad*Wp); Z=t[:,:,2]
    n=np.cross(t[:,1]-t[:,0],t[:,2]-t[:,0]); l=np.linalg.norm(n,axis=1); l[l==0]=1; n/=l[:,None]
    n[n[:,2]<0]*=-1
    L=np.array([-0.35,0.45,0.82]); L/=np.linalg.norm(L)
    sh=0.30+0.70*np.clip(n@L,0,1)
    zb=np.full((Hp,Wq),-1e18); img=np.zeros((Hp,Wq)); 
    x0=np.floor(X.min(1)).astype(int).clip(0,Wq-1); x1=np.ceil(X.max(1)).astype(int).clip(0,Wq-1)
    y0=np.floor(Y.min(1)).astype(int).clip(0,Hp-1); y1=np.ceil(Y.max(1)).astype(int).clip(0,Hp-1)
    den=(Y[:,1]-Y[:,2])*(X[:,0]-X[:,2])+(X[:,2]-X[:,1])*(Y[:,0]-Y[:,2])
    for i in range(len(t)):
        if abs(den[i])<1e-12: continue
        xs=np.arange(x0[i],x1[i]+1)+0.5; ys=np.arange(y0[i],y1[i]+1)+0.5
        gx,gy=np.meshgrid(xs,ys)
        a=((Y[i,1]-Y[i,2])*(gx-X[i,2])+(X[i,2]-X[i,1])*(gy-Y[i,2]))/den[i]
        b=((Y[i,2]-Y[i,0])*(gx-X[i,2])+(X[i,0]-X[i,2])*(gy-Y[i,2]))/den[i]
        c=1-a-b
        m=(a>=-1e-6)&(b>=-1e-6)&(c>=-1e-6)
        if not m.any(): continue
        z=a*Z[i,0]+b*Z[i,1]+c*Z[i,2]
        sub=zb[y0[i]:y1[i]+1,x0[i]:x1[i]+1]; si=img[y0[i]:y1[i]+1,x0[i]:x1[i]+1]
        m&=z>sub
        sub[m]=z[m]; si[m]=sh[i]
    bg=zb<-1e17
    zz=np.where(bg,zb[~bg].min()-5,zb)
    gy,gx=np.gradient(zz); edge=np.clip(np.hypot(gx,gy)/ (0.9),0,1)
    gsy,gsx=np.gradient(img); edge2=np.clip(np.hypot(gsx,gsy)*3,0,1)
    e=np.clip(np.maximum(edge,edge2*0.6),0,1)
    base=np.stack([img*0.50+0.16,img*0.62+0.18,img*0.80+0.18],2)
    base=base*(1-0.75*e[:,:,None])
    base[bg]=1.0
    # outline on bg border
    im=Image.fromarray((np.clip(base,0,1)*255).astype(np.uint8)).resize((Wq//ss,Hp//ss),Image.LANCZOS)
    im.save(out); print(out, im.size)
views={"35-60mm_Hook_U_BPW_Box_V13":[(35,-62),(215,-62),(90,-90)],"BPW_Head_V2_Final":[(35,-55),(35,-125)],"Cap":[(35,-60)],"ESP_doboz_teto_v6":[(30,-50)],
 "Screw_35-60mm_plus_10mm":[(35,-70)],"esp_doboz_usb_huvely":[(35,-60),(215,-60)],"esp_doboz_v6":[(30,-50),(210,-50)],"zaroklip_v4":[(30,-50)]}
only=sys.argv[2:] 
for f,vs in views.items():
    if only and f not in only: continue
    tri=load(os.path.join(d,f+".stl"))
    for k,(az,el) in enumerate(vs):
        render(tri,os.path.join(S,"out",f"r_{f}_{k}.png"),az,el,W=900 if len(tri)<50000 else 800)
