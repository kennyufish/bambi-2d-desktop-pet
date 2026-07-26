import { CatBuilder } from "./cat-builder";

export default function Home() {
  return (
    <main>
      <header className="site-header">
        <a className="wordmark" href="#top" aria-label="你的猫 3D 桌宠首页">
          <span className="paw-dot" aria-hidden="true">●</span>
          你的猫
        </a>
        <nav aria-label="主导航">
          <a href="#how">生成流程</a>
          <a href="#builder">创建预览</a>
          <a href="#faq">常见问题</a>
        </nav>
      </header>

      <section className="hero" id="top">
        <div className="hero-copy">
          <p className="eyebrow">多角度照片 · 专属 3D 桌宠</p>
          <h1>把你家的猫，<br />带到桌面上。</h1>
          <p className="hero-lede">
            上传正面、侧面和背面照片。我们保留它的花纹、脸型、眼睛与体态，
            做成会走、会睡、会回应你的 3D 小伙伴。
          </p>
          <div className="hero-actions">
            <a className="primary-button" href="#builder">免费创建静态预览</a>
            <a className="text-link" href="#how">看看如何生成 <span>→</span></a>
          </div>
          <p className="trust-note">预览满意后再付费 · 原始照片于订单完成 7 天后删除</p>
        </div>

        <div className="hero-visual" aria-label="三张猫咪照片转换为桌面上的三维猫咪示意图">
          <div className="photo-stack" aria-hidden="true">
            <div className="cat-photo front"><span>正面</span></div>
            <div className="cat-photo side"><span>侧面</span></div>
            <div className="cat-photo back"><span>背面</span></div>
          </div>
          <div className="conversion-arrow" aria-hidden="true">→</div>
          <div className="desktop-frame">
            <div className="desktop-topbar"><i /><i /><i /></div>
            <div className="desktop-scene">
              <div className="cat-figure" aria-hidden="true">
                <div className="cat-tail" />
                <div className="cat-body" />
                <div className="cat-head">
                  <div className="ear left-ear" />
                  <div className="ear right-ear" />
                  <div className="eye left-eye" />
                  <div className="eye right-eye" />
                  <div className="muzzle" />
                </div>
                <div className="leg leg-one" />
                <div className="leg leg-two" />
              </div>
              <div className="taskbar" />
            </div>
          </div>
        </div>
      </section>

      <section className="how-section" id="how">
        <div>
          <p className="eyebrow">不是从零乱猜</p>
          <h2>稳定骨架，定制外观</h2>
        </div>
        <div className="steps">
          <article><b>01</b><h3>上传 3–5 张照片</h3><p>清晰的正面、左右侧面和背面，让花纹与体型都有依据。</p></article>
          <article><b>02</b><h3>选择体型并微调</h3><p>从五种标准猫体型开始，调整胖瘦、脸宽、耳朵与腿长。</p></article>
          <article><b>03</b><h3>确认后生成</h3><p>先看免费静态预览。满意并付款后，5–10 分钟生成完整桌宠。</p></article>
        </div>
      </section>

      <CatBuilder />

      <section className="promise-section">
        <p className="eyebrow">首版承诺</p>
        <h2>先把“像它”做好，再让它动起来。</h2>
        <div className="promise-grid">
          <div><strong>5</strong><span>种标准体型</span></div>
          <div><strong>7</strong><span>类桌面互动</span></div>
          <div><strong>2</strong><span>次付费后局部修正</span></div>
          <div><strong>7 天</strong><span>后删除原始照片</span></div>
        </div>
      </section>

      <section className="faq-section" id="faq">
        <div><p className="eyebrow">常见问题</p><h2>生成前，你可能想知道</h2></div>
        <div className="faq-list">
          <details open><summary>为什么需要多张照片？</summary><p>单张照片看不到背面和另一侧花纹。多角度输入能减少 AI 猜测，让结果更像同一只猫。</p></details>
          <details><summary>支持哪些猫？</summary><p>首版支持无遮挡、未穿衣服的常见短毛和毛绒化长毛猫。无毛猫及需要改变标准骨架的特殊身体情况暂不支持。</p></details>
          <details><summary>付费后还能修改吗？</summary><p>身材滑块可以继续调整；花纹、眼睛或脸部纹理提供两次局部重新生成。更换照片或猫咪会视为新订单。</p></details>
          <details><summary>桌宠会做什么？</summary><p>它会走动、坐下、趴下、睡觉，回应抚摸和拖拽，追逐鼠标并接受喂食。</p></details>
        </div>
      </section>

      <footer>
        <span>你的猫 · 3D 桌宠</span>
        <span>Windows 10 / 11 · 正在开发首个验证版本</span>
      </footer>
    </main>
  );
}
